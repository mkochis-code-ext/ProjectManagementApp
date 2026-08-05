using System.Text.Json;
using ProjectManagementApp.Models;

namespace ProjectManagementApp.Services;

public class BoardService : IBoardService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BoardCollection _collection = new();

    public BoardService()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _filePath = Path.Combine(documentsPath, "BoardCollection.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<BoardCollection> LoadCollectionAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _collection = JsonSerializer.Deserialize<BoardCollection>(json, _jsonOptions) ?? new BoardCollection();

                // Migrate old data: ensure all boards have required properties
                foreach (var board in _collection.Boards)
                {
                    board.Todos ??= new List<TodoItem>();

                    // Migrate cards: ensure Notes and Links exist
                    foreach (var lane in board.Lanes)
                    {
                        foreach (var card in lane.Cards)
                        {
                            card.Notes ??= string.Empty;
                            card.Links ??= new List<CardLink>();
                            card.Contacts ??= new List<CardContact>();
                            // Migrate todos: ensure Notes exist
                            foreach (var todo in card.Todos)
                            {
                                todo.Notes ??= string.Empty;
                            }
                        }
                    }
                    // Migrate board-level todos: ensure Notes exist
                    foreach (var todo in board.Todos)
                    {
                        todo.Notes ??= string.Empty;
                    }
                }
            }
            else
            {
                _collection = new BoardCollection();
            }
            return _collection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveCollectionAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_collection, _jsonOptions);
            // Atomic write: write to a temp file, then replace the target so a
            // concurrent reader never sees a partially-written file.
            var tempPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<Board>> GetAllBoardsAsync()
    {
        await LoadCollectionAsync();
        return _collection.Boards;
    }

    public async Task<Board?> GetBoardAsync(Guid boardId)
    {
        await LoadCollectionAsync();
        return _collection.Boards.FirstOrDefault(b => b.Id == boardId);
    }

    public async Task<Board?> GetLastOpenedBoardAsync()
    {
        await LoadCollectionAsync();
        Board? board = null;
        if (_collection.LastOpenedBoardId.HasValue)
        {
            board = _collection.Boards.FirstOrDefault(b => b.Id == _collection.LastOpenedBoardId.Value);
        }
        board ??= _collection.Boards.FirstOrDefault();
        return board;
    }

    public async Task<List<Board>> SearchBoardsAsync(string query)
    {
        await LoadCollectionAsync();
        return _collection.Boards.Where(b => Matches(b.Name, query)).ToList();
    }

    public async Task<List<LaneSearchResult>> SearchLanesAsync(string query, Guid? boardId = null)
    {
        await LoadCollectionAsync();
        var results = new List<LaneSearchResult>();
        foreach (var board in BoardsInScope(boardId))
        {
            foreach (var lane in board.Lanes)
            {
                if (Matches(lane.Name, query))
                    results.Add(new LaneSearchResult(board.Id, board.Name, lane));
            }
        }
        return results;
    }

    public async Task<List<CardSearchResult>> SearchCardsAsync(string query, Guid? boardId = null)
    {
        await LoadCollectionAsync();
        var results = new List<CardSearchResult>();
        foreach (var board in BoardsInScope(boardId))
        {
            foreach (var lane in board.Lanes)
            {
                foreach (var card in lane.Cards)
                {
                    if (Matches(card.Title, query))
                        results.Add(new CardSearchResult(board.Id, board.Name, lane.Id, lane.Name, card));
                }
            }
        }
        return results;
    }

    public async Task<List<TodoSearchResult>> SearchTodosAsync(string query, Guid? boardId = null)
    {
        await LoadCollectionAsync();
        var results = new List<TodoSearchResult>();
        foreach (var board in BoardsInScope(boardId))
        {
            foreach (var todo in board.Todos)
            {
                if (Matches(todo.Text, query))
                    results.Add(new TodoSearchResult(board.Id, board.Name, null, null, null, null, todo));
            }
            foreach (var lane in board.Lanes)
            {
                foreach (var card in lane.Cards)
                {
                    foreach (var todo in card.Todos)
                    {
                        if (Matches(todo.Text, query))
                            results.Add(new TodoSearchResult(board.Id, board.Name, lane.Id, lane.Name, card.Id, card.Title, todo));
                    }
                }
            }
        }
        return results;
    }

    public async Task<List<TodoSearchResult>> GetOpenTodosAsync(string query, bool todayOnly = false)
    {
        await LoadCollectionAsync();
        var results = new List<TodoSearchResult>();
        foreach (var board in _collection.Boards)
        {
            foreach (var todo in board.Todos)
            {
                if (IsOpenTodoMatch(todo, query, todayOnly))
                    results.Add(new TodoSearchResult(board.Id, board.Name, null, null, null, null, todo));
            }
            foreach (var lane in board.Lanes)
            {
                foreach (var card in lane.Cards)
                {
                    foreach (var todo in card.Todos)
                    {
                        if (IsOpenTodoMatch(todo, query, todayOnly))
                            results.Add(new TodoSearchResult(board.Id, board.Name, lane.Id, lane.Name, card.Id, card.Title, todo));
                    }
                }
            }
        }
        return results;
    }

    private static bool IsOpenTodoMatch(TodoItem todo, string query, bool todayOnly) =>
        !todo.IsCompleted && (!todayOnly || todo.IsTodaysTodo) && Matches(todo.Text, query);

    private IEnumerable<Board> BoardsInScope(Guid? boardId) =>
        boardId is null ? _collection.Boards : _collection.Boards.Where(b => b.Id == boardId.Value);

    private static bool Matches(string value, string query) =>
        string.IsNullOrWhiteSpace(query) || value.Contains(query, StringComparison.OrdinalIgnoreCase);

    public async Task SetLastOpenedBoardAsync(Guid boardId)
    {
        _collection.LastOpenedBoardId = boardId;
        await SaveCollectionAsync();
    }

    public async Task<Board> CreateBoardAsync(string name, string laneLabel = "Lane", string cardLabel = "Card", string todoLabel = "Todo")
    {
        var board = new Board
        {
            Name = name,
            LaneLabel = laneLabel,
            CardLabel = cardLabel,
            TodoLabel = todoLabel
        };
        _collection.Boards.Add(board);
        _collection.LastOpenedBoardId = board.Id;
        await SaveCollectionAsync();
        return board;
    }

    public async Task UpdateBoardAsync(Guid boardId, string name, string laneLabel, string cardLabel, string todoLabel)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board != null)
        {
            board.Name = name;
            board.LaneLabel = laneLabel;
            board.CardLabel = cardLabel;
            board.TodoLabel = todoLabel;
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task SetBoardViewModeAsync(Guid boardId, bool isCondensed)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board != null)
        {
            board.IsCondensedView = isCondensed;
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task DeleteBoardAsync(Guid boardId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board != null)
        {
            _collection.Boards.Remove(board);
            if (_collection.LastOpenedBoardId == boardId)
            {
                _collection.LastOpenedBoardId = _collection.Boards.FirstOrDefault()?.Id;
            }
            await SaveCollectionAsync();
        }
    }

    // Lane operations
    public async Task<Lane> AddLaneAsync(Guid boardId, string name)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board != null)
        {
            var lane = new Lane
            {
                Name = name,
                Order = board.Lanes.Count
            };
            board.Lanes.Add(lane);
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return lane;
        }
        return null!;
    }

    public async Task UpdateLaneAsync(Guid boardId, Guid laneId, string name)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        if (lane != null)
        {
            lane.Name = name;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task SetLaneArchivedAsync(Guid boardId, Guid laneId, bool isArchived)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        if (lane != null)
        {
            lane.IsArchived = isArchived;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task MoveLaneAsync(Guid boardId, Guid laneId, int direction)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board == null || direction == 0) return;

        var ordered = board.Lanes.OrderBy(l => l.Order).ToList();
        var index = ordered.FindIndex(l => l.Id == laneId);
        if (index < 0) return;

        var targetIndex = index + Math.Sign(direction);
        if (targetIndex < 0 || targetIndex >= ordered.Count) return;

        // Normalize orders to indices, then swap with the neighbor.
        for (var i = 0; i < ordered.Count; i++) ordered[i].Order = i;
        (ordered[index].Order, ordered[targetIndex].Order) = (ordered[targetIndex].Order, ordered[index].Order);

        board.LastModified = DateTime.UtcNow;
        await SaveCollectionAsync();
    }

    public async Task DeleteLaneAsync(Guid boardId, Guid laneId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        if (lane != null)
        {
            board!.Lanes.Remove(lane);
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Card operations
    public async Task<Card> AddCardAsync(Guid boardId, Guid laneId, string title, string description)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        if (lane != null)
        {
            var card = new Card
            {
                Title = title,
                Description = description,
                Order = lane.Cards.Count
            };
            lane.Cards.Add(card);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return card;
        }
        return null!;
    }

    public async Task UpdateCardAsync(Guid boardId, Guid laneId, Guid cardId, string title, string description)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            card.Title = title;
            card.Description = description;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task SetCardCompletionAsync(Guid boardId, Guid laneId, Guid cardId, bool isCompleted)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            card.IsCompleted = isCompleted;
            card.CompletedAt = isCompleted ? DateTime.UtcNow : null;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task MoveCardAsync(Guid boardId, Guid fromLaneId, Guid toLaneId, Guid cardId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var fromLane = board?.Lanes.FirstOrDefault(l => l.Id == fromLaneId);
        var toLane = board?.Lanes.FirstOrDefault(l => l.Id == toLaneId);
        var card = fromLane?.Cards.FirstOrDefault(c => c.Id == cardId);

        if (card != null && toLane != null && fromLane != null)
        {
            fromLane.Cards.Remove(card);
            card.Order = toLane.Cards.Count;
            toLane.Cards.Add(card);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task DeleteCardAsync(Guid boardId, Guid laneId, Guid cardId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            lane!.Cards.Remove(card);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Card notes operations
    public async Task UpdateCardNotesAsync(Guid boardId, Guid laneId, Guid cardId, string notes)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            card.Notes = notes;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Card links operations
    public async Task<CardLink> AddCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, string title, string url)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            var link = new CardLink
            {
                Title = title,
                Url = url
            };
            card.Links.Add(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return link;
        }
        return null!;
    }

    public async Task DeleteCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid linkId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var link = card?.Links.FirstOrDefault(l => l.Id == linkId);
        if (link != null)
        {
            card!.Links.Remove(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Card contact operations
    public async Task<CardContact> AddCardContactAsync(Guid boardId, Guid laneId, Guid cardId, string name, string email)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            var contact = new CardContact
            {
                Name = name,
                Email = email
            };
            card.Contacts.Add(contact);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return contact;
        }
        return null!;
    }

    public async Task DeleteCardContactAsync(Guid boardId, Guid laneId, Guid cardId, Guid contactId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var contact = card?.Contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact != null)
        {
            card!.Contacts.Remove(contact);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Todo operations
    public async Task<TodoItem> AddTodoAsync(Guid boardId, Guid laneId, Guid cardId, string text, bool isTodaysTodo = false, string notes = "")
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            var todo = new TodoItem
            {
                Text = text,
                Order = card.Todos.Count,
                IsTodaysTodo = isTodaysTodo,
                Notes = notes
            };
            card.Todos.Add(todo);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return todo;
        }
        return null!;
    }

    public async Task UpdateTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string text)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.Text = text;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task SetTodoCompletionAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isCompleted)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.IsCompleted = isCompleted;
            todo.CompletedAt = isCompleted ? DateTime.UtcNow : null;
            if (isCompleted) todo.IsTodaysTodo = false;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task DeleteTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            card!.Todos.Remove(todo);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Board-level todo operations
    public async Task<TodoItem> AddBoardTodoAsync(Guid boardId, string text, bool isTodaysTodo = false, string notes = "")
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        if (board != null)
        {
            var todo = new TodoItem
            {
                Text = text,
                Order = board.Todos.Count,
                IsTodaysTodo = isTodaysTodo,
                Notes = notes
            };
            board.Todos.Add(todo);
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return todo;
        }
        return null!;
    }

    public async Task SetBoardTodoCompletionAsync(Guid boardId, Guid todoId, bool isCompleted)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.IsCompleted = isCompleted;
            todo.CompletedAt = isCompleted ? DateTime.UtcNow : null;
            if (isCompleted) todo.IsTodaysTodo = false;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task DeleteBoardTodoAsync(Guid boardId, Guid todoId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            board!.Todos.Remove(todo);
            board.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Today's todo operations
    public async Task SetTodaysTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isTodaysTodo)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.IsTodaysTodo = isTodaysTodo;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task SetBoardTodaysTodoAsync(Guid boardId, Guid todoId, bool isTodaysTodo)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.IsTodaysTodo = isTodaysTodo;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Todo notes operations
    public async Task UpdateTodoNotesAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string notes)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.Notes = notes;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task UpdateBoardTodoNotesAsync(Guid boardId, Guid todoId, string notes)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.Notes = notes;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Todo text operations
    public async Task UpdateTodoTextAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string text)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.Text = text;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    public async Task UpdateBoardTodoTextAsync(Guid boardId, Guid todoId, string text)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            todo.Text = text;
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Todo link operations (card-level todos)
    public async Task<TodoLink> AddTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string title, string url)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            var link = new TodoLink { Title = title, Url = url };
            todo.Links.Add(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return link;
        }
        return null!;
    }

    public async Task DeleteTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, Guid linkId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var lane = board?.Lanes.FirstOrDefault(l => l.Id == laneId);
        var card = lane?.Cards.FirstOrDefault(c => c.Id == cardId);
        var todo = card?.Todos.FirstOrDefault(t => t.Id == todoId);
        var link = todo?.Links.FirstOrDefault(l => l.Id == linkId);
        if (link != null)
        {
            todo!.Links.Remove(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }

    // Todo link operations (board-level todos)
    public async Task<TodoLink> AddBoardTodoLinkAsync(Guid boardId, Guid todoId, string title, string url)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        if (todo != null)
        {
            var link = new TodoLink { Title = title, Url = url };
            todo.Links.Add(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
            return link;
        }
        return null!;
    }

    public async Task DeleteBoardTodoLinkAsync(Guid boardId, Guid todoId, Guid linkId)
    {
        var board = _collection.Boards.FirstOrDefault(b => b.Id == boardId);
        var todo = board?.Todos.FirstOrDefault(t => t.Id == todoId);
        var link = todo?.Links.FirstOrDefault(l => l.Id == linkId);
        if (link != null)
        {
            todo!.Links.Remove(link);
            board!.LastModified = DateTime.UtcNow;
            await SaveCollectionAsync();
        }
    }
}
