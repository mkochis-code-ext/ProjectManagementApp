using System.Text.Json;
using ProjectManagementApp.Models;
using ProjectManagementApp.Services;

namespace ProjectManagementApp.Tests;

/// <summary>
/// A testable BoardService that uses a temp directory instead of MyDocuments.
/// Uses reflection to swap the private _filePath field.
/// </summary>
public class TestBoardService : BoardService, IDisposable
{
    private readonly string _tempDir;

    public TestBoardService()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BoardServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Replace _filePath via reflection so all file I/O targets the temp dir
        var field = typeof(BoardService).GetField("_filePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(this, Path.Combine(_tempDir, "BoardCollection.json"));
    }

    public string FilePath => Path.Combine(_tempDir, "BoardCollection.json");

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}

public class BoardServiceTests : IDisposable
{
    private readonly TestBoardService _service;

    public BoardServiceTests()
    {
        _service = new TestBoardService();
    }

    public void Dispose() => _service.Dispose();

    // ── Load / Save ──────────────────────────────────────────────

    [Fact]
    public async Task LoadCollectionAsync_ReturnsEmptyCollection_WhenFileDoesNotExist()
    {
        var collection = await _service.LoadCollectionAsync();

        Assert.NotNull(collection);
        Assert.Empty(collection.Boards);
        Assert.Null(collection.LastOpenedBoardId);
    }

    [Fact]
    public async Task LoadCollectionAsync_ReadsExistingFile()
    {
        // Arrange – write a file manually
        var board = new Board { Name = "Persisted" };
        var data = new BoardCollection { Boards = [board], LastOpenedBoardId = board.Id };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(_service.FilePath, json);

        // Act
        var collection = await _service.LoadCollectionAsync();

        // Assert
        Assert.Single(collection.Boards);
        Assert.Equal("Persisted", collection.Boards[0].Name);
        Assert.Equal(board.Id, collection.LastOpenedBoardId);
    }

    [Fact]
    public async Task LoadCollectionAsync_MigratesNullTodosAndLinks()
    {
        // Arrange – write JSON where card.Notes, card.Links, and board.Todos are null
        var json = """
        {
            "boards": [{
                "id": "00000000-0000-0000-0000-000000000001",
                "name": "Old Board",
                "laneLabel": "Lane",
                "cardLabel": "Card",
                "todoLabel": "Todo",
                "lanes": [{
                    "id": "00000000-0000-0000-0000-000000000002",
                    "name": "Lane 1",
                    "order": 0,
                    "createdAt": "2025-01-01T00:00:00Z",
                    "cards": [{
                        "id": "00000000-0000-0000-0000-000000000003",
                        "title": "Old Card",
                        "description": "",
                        "isCompleted": false,
                        "createdAt": "2025-01-01T00:00:00Z",
                        "order": 0,
                        "todos": []
                    }]
                }],
                "createdAt": "2025-01-01T00:00:00Z",
                "lastModified": "2025-01-01T00:00:00Z"
            }],
            "lastOpenedBoardId": null
        }
        """;
        await File.WriteAllTextAsync(_service.FilePath, json);

        // Act
        var collection = await _service.LoadCollectionAsync();

        // Assert – migration should fill in nulls
        var card = collection.Boards[0].Lanes[0].Cards[0];
        Assert.NotNull(card.Notes);
        Assert.NotNull(card.Links);
        Assert.NotNull(collection.Boards[0].Todos);
    }

    [Fact]
    public async Task SaveCollectionAsync_CreatesFile()
    {
        await _service.CreateBoardAsync("Save Test");

        Assert.True(File.Exists(_service.FilePath));
        var json = await File.ReadAllTextAsync(_service.FilePath);
        Assert.Contains("Save Test", json);
    }

    // ── Board CRUD ───────────────────────────────────────────────

    [Fact]
    public async Task CreateBoardAsync_AddsBoardAndSetsLastOpened()
    {
        var board = await _service.CreateBoardAsync("My Board", "Column", "Item", "Task");

        Assert.Equal("My Board", board.Name);
        Assert.Equal("Column", board.LaneLabel);
        Assert.Equal("Item", board.CardLabel);
        Assert.Equal("Task", board.TodoLabel);

        var boards = await _service.GetAllBoardsAsync();
        Assert.Single(boards);
    }

    [Fact]
    public async Task CreateBoardAsync_UsesDefaultLabels()
    {
        var board = await _service.CreateBoardAsync("Defaults");

        Assert.Equal("Lane", board.LaneLabel);
        Assert.Equal("Card", board.CardLabel);
        Assert.Equal("Todo", board.TodoLabel);
    }

    [Fact]
    public async Task GetAllBoardsAsync_ReturnsAllBoards()
    {
        await _service.CreateBoardAsync("Board 1");
        await _service.CreateBoardAsync("Board 2");
        await _service.CreateBoardAsync("Board 3");

        var boards = await _service.GetAllBoardsAsync();
        Assert.Equal(3, boards.Count);
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsCorrectBoard()
    {
        var created = await _service.CreateBoardAsync("Find Me");

        var found = await _service.GetBoardAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal("Find Me", found!.Name);
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsNull_WhenNotFound()
    {
        var found = await _service.GetBoardAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task GetLastOpenedBoardAsync_ReturnsLastOpened()
    {
        var board1 = await _service.CreateBoardAsync("First");
        var board2 = await _service.CreateBoardAsync("Second");
        await _service.SetLastOpenedBoardAsync(board1.Id);

        var last = await _service.GetLastOpenedBoardAsync();

        Assert.NotNull(last);
        Assert.Equal("First", last!.Name);
    }

    [Fact]
    public async Task GetLastOpenedBoardAsync_FallsBackToFirstBoard()
    {
        await _service.CreateBoardAsync("Only Board");

        // Force LastOpenedBoardId to a non-existent guid
        await _service.SetLastOpenedBoardAsync(Guid.NewGuid());

        var last = await _service.GetLastOpenedBoardAsync();

        Assert.NotNull(last);
        Assert.Equal("Only Board", last!.Name);
    }

    [Fact]
    public async Task GetLastOpenedBoardAsync_ReturnsNull_WhenNoBoards()
    {
        var last = await _service.GetLastOpenedBoardAsync();
        Assert.Null(last);
    }

    [Fact]
    public async Task UpdateBoardAsync_UpdatesProperties()
    {
        var board = await _service.CreateBoardAsync("Original");
        var beforeModified = board.LastModified;

        await _service.UpdateBoardAsync(board.Id, "Updated", "Col", "Item", "Task");

        var updated = await _service.GetBoardAsync(board.Id);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal("Col", updated.LaneLabel);
        Assert.Equal("Item", updated.CardLabel);
        Assert.Equal("Task", updated.TodoLabel);
        Assert.True(updated.LastModified >= beforeModified);
    }

    [Fact]
    public async Task UpdateBoardAsync_NoOp_WhenBoardNotFound()
    {
        // Should not throw
        await _service.UpdateBoardAsync(Guid.NewGuid(), "X", "X", "X", "X");
    }

    [Fact]
    public async Task DeleteBoardAsync_RemovesBoard()
    {
        var board = await _service.CreateBoardAsync("To Delete");

        await _service.DeleteBoardAsync(board.Id);

        var boards = await _service.GetAllBoardsAsync();
        Assert.Empty(boards);
    }

    [Fact]
    public async Task DeleteBoardAsync_ClearsLastOpened_WhenDeletingLastOpened()
    {
        var board1 = await _service.CreateBoardAsync("Keep");
        var board2 = await _service.CreateBoardAsync("Delete");
        await _service.SetLastOpenedBoardAsync(board2.Id);

        await _service.DeleteBoardAsync(board2.Id);

        var last = await _service.GetLastOpenedBoardAsync();
        Assert.NotNull(last);
        Assert.Equal("Keep", last!.Name);
    }

    [Fact]
    public async Task DeleteBoardAsync_NoOp_WhenBoardNotFound()
    {
        await _service.CreateBoardAsync("Exists");
        await _service.DeleteBoardAsync(Guid.NewGuid());

        var boards = await _service.GetAllBoardsAsync();
        Assert.Single(boards);
    }

    // ── Lane CRUD ────────────────────────────────────────────────

    [Fact]
    public async Task AddLaneAsync_AddsLaneToBoard()
    {
        var board = await _service.CreateBoardAsync("Board");

        var lane = await _service.AddLaneAsync(board.Id, "To Do");

        Assert.NotNull(lane);
        Assert.Equal("To Do", lane.Name);
        Assert.Equal(0, lane.Order);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes);
    }

    [Fact]
    public async Task AddLaneAsync_SetsIncrementingOrder()
    {
        var board = await _service.CreateBoardAsync("Board");

        var lane1 = await _service.AddLaneAsync(board.Id, "First");
        var lane2 = await _service.AddLaneAsync(board.Id, "Second");

        Assert.Equal(0, lane1.Order);
        Assert.Equal(1, lane2.Order);
    }

    [Fact]
    public async Task AddLaneAsync_ReturnsNull_WhenBoardNotFound()
    {
        var result = await _service.AddLaneAsync(Guid.NewGuid(), "Orphan");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLaneAsync_UpdatesName()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Old Name");

        await _service.UpdateLaneAsync(board.Id, lane.Id, "New Name");

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Equal("New Name", loaded!.Lanes[0].Name);
    }

    [Fact]
    public async Task UpdateLaneAsync_NoOp_WhenLaneNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        await _service.UpdateLaneAsync(board.Id, Guid.NewGuid(), "X");

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes);
    }

    [Fact]
    public async Task DeleteLaneAsync_RemovesLane()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Delete Me");

        await _service.DeleteLaneAsync(board.Id, lane.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes);
    }

    [Fact]
    public async Task DeleteLaneAsync_NoOp_WhenLaneNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        await _service.AddLaneAsync(board.Id, "Keep");

        await _service.DeleteLaneAsync(board.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes);
    }

    // ── Card CRUD ────────────────────────────────────────────────

    [Fact]
    public async Task AddCardAsync_AddsCardToLane()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        var card = await _service.AddCardAsync(board.Id, lane.Id, "Task", "Do things");

        Assert.NotNull(card);
        Assert.Equal("Task", card.Title);
        Assert.Equal("Do things", card.Description);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards);
    }

    [Fact]
    public async Task AddCardAsync_SetsIncrementingOrder()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        var card1 = await _service.AddCardAsync(board.Id, lane.Id, "First", "");
        var card2 = await _service.AddCardAsync(board.Id, lane.Id, "Second", "");

        Assert.Equal(0, card1.Order);
        Assert.Equal(1, card2.Order);
    }

    [Fact]
    public async Task AddCardAsync_ReturnsNull_WhenLaneNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var result = await _service.AddCardAsync(board.Id, Guid.NewGuid(), "X", "X");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCardAsync_UpdatesTitleAndDescription()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Old", "Old Desc");

        await _service.UpdateCardAsync(board.Id, lane.Id, card.Id, "New", "New Desc");

        var loaded = await _service.GetBoardAsync(board.Id);
        var updated = loaded!.Lanes[0].Cards[0];
        Assert.Equal("New", updated.Title);
        Assert.Equal("New Desc", updated.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_NoOp_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        await _service.UpdateCardAsync(board.Id, lane.Id, Guid.NewGuid(), "X", "X");

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes[0].Cards);
    }

    [Fact]
    public async Task SetCardCompletionAsync_MarksComplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Task", "");

        await _service.SetCardCompletionAsync(board.Id, lane.Id, card.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Lanes[0].Cards[0].IsCompleted);
        Assert.NotNull(loaded.Lanes[0].Cards[0].CompletedAt);
    }

    [Fact]
    public async Task SetCardCompletionAsync_MarksIncomplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Task", "");

        await _service.SetCardCompletionAsync(board.Id, lane.Id, card.Id, true);
        await _service.SetCardCompletionAsync(board.Id, lane.Id, card.Id, false);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.False(loaded!.Lanes[0].Cards[0].IsCompleted);
        Assert.Null(loaded.Lanes[0].Cards[0].CompletedAt);
    }

    [Fact]
    public async Task MoveCardAsync_MovesCardBetweenLanes()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane1 = await _service.AddLaneAsync(board.Id, "From");
        var lane2 = await _service.AddLaneAsync(board.Id, "To");
        var card = await _service.AddCardAsync(board.Id, lane1.Id, "Move Me", "");

        await _service.MoveCardAsync(board.Id, lane1.Id, lane2.Id, card.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes.First(l => l.Id == lane1.Id).Cards);
        Assert.Single(loaded.Lanes.First(l => l.Id == lane2.Id).Cards);
        Assert.Equal("Move Me", loaded.Lanes.First(l => l.Id == lane2.Id).Cards[0].Title);
    }

    [Fact]
    public async Task MoveCardAsync_UpdatesOrder()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane1 = await _service.AddLaneAsync(board.Id, "From");
        var lane2 = await _service.AddLaneAsync(board.Id, "To");
        await _service.AddCardAsync(board.Id, lane2.Id, "Existing", "");
        var card = await _service.AddCardAsync(board.Id, lane1.Id, "Mover", "");

        await _service.MoveCardAsync(board.Id, lane1.Id, lane2.Id, card.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        var movedCard = loaded!.Lanes.First(l => l.Id == lane2.Id).Cards.First(c => c.Title == "Mover");
        Assert.Equal(1, movedCard.Order);
    }

    [Fact]
    public async Task MoveCardAsync_NoOp_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane1 = await _service.AddLaneAsync(board.Id, "From");
        var lane2 = await _service.AddLaneAsync(board.Id, "To");

        await _service.MoveCardAsync(board.Id, lane1.Id, lane2.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes[0].Cards);
        Assert.Empty(loaded.Lanes[1].Cards);
    }

    [Fact]
    public async Task DeleteCardAsync_RemovesCard()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Delete Me", "");

        await _service.DeleteCardAsync(board.Id, lane.Id, card.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes[0].Cards);
    }

    [Fact]
    public async Task DeleteCardAsync_NoOp_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        await _service.AddCardAsync(board.Id, lane.Id, "Keep", "");

        await _service.DeleteCardAsync(board.Id, lane.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards);
    }

    // ── Card Notes ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateCardNotesAsync_UpdatesNotes()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        await _service.UpdateCardNotesAsync(board.Id, lane.Id, card.Id, "Some notes here");

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Equal("Some notes here", loaded!.Lanes[0].Cards[0].Notes);
    }

    [Fact]
    public async Task UpdateCardNotesAsync_NoOp_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        await _service.UpdateCardNotesAsync(board.Id, lane.Id, Guid.NewGuid(), "Notes");
    }

    // ── Card Links ───────────────────────────────────────────────

    [Fact]
    public async Task AddCardLinkAsync_AddsLink()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        var link = await _service.AddCardLinkAsync(board.Id, lane.Id, card.Id, "Docs", "https://docs.example.com");

        Assert.NotNull(link);
        Assert.Equal("Docs", link.Title);
        Assert.Equal("https://docs.example.com", link.Url);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards[0].Links);
    }

    [Fact]
    public async Task AddCardLinkAsync_ReturnsNull_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        var result = await _service.AddCardLinkAsync(board.Id, lane.Id, Guid.NewGuid(), "X", "X");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteCardLinkAsync_RemovesLink()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var link = await _service.AddCardLinkAsync(board.Id, lane.Id, card.Id, "Link", "https://x.com");

        await _service.DeleteCardLinkAsync(board.Id, lane.Id, card.Id, link.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes[0].Cards[0].Links);
    }

    [Fact]
    public async Task DeleteCardLinkAsync_NoOp_WhenLinkNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        await _service.AddCardLinkAsync(board.Id, lane.Id, card.Id, "Keep", "https://x.com");

        await _service.DeleteCardLinkAsync(board.Id, lane.Id, card.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards[0].Links);
    }

    // ── Card Todos ───────────────────────────────────────────────

    [Fact]
    public async Task AddTodoAsync_AddsTodoToCard()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do this");

        Assert.NotNull(todo);
        Assert.Equal("Do this", todo.Text);
        Assert.Equal(0, todo.Order);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards[0].Todos);
    }

    [Fact]
    public async Task AddTodoAsync_SetsIncrementingOrder()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        var todo1 = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "First");
        var todo2 = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Second");

        Assert.Equal(0, todo1.Order);
        Assert.Equal(1, todo2.Order);
    }

    [Fact]
    public async Task AddTodoAsync_ReturnsNull_WhenCardNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");

        var result = await _service.AddTodoAsync(board.Id, lane.Id, Guid.NewGuid(), "X");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTodoAsync_UpdatesText()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Old");

        await _service.UpdateTodoAsync(board.Id, lane.Id, card.Id, todo.Id, "New");

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Equal("New", loaded!.Lanes[0].Cards[0].Todos[0].Text);
    }

    [Fact]
    public async Task UpdateTodoAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        await _service.UpdateTodoAsync(board.Id, lane.Id, card.Id, Guid.NewGuid(), "X");
    }

    [Fact]
    public async Task SetTodoCompletionAsync_MarksComplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do");

        await _service.SetTodoCompletionAsync(board.Id, lane.Id, card.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Lanes[0].Cards[0].Todos[0].IsCompleted);
        Assert.NotNull(loaded.Lanes[0].Cards[0].Todos[0].CompletedAt);
    }

    [Fact]
    public async Task SetTodoCompletionAsync_MarksIncomplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do");

        await _service.SetTodoCompletionAsync(board.Id, lane.Id, card.Id, todo.Id, true);
        await _service.SetTodoCompletionAsync(board.Id, lane.Id, card.Id, todo.Id, false);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.False(loaded!.Lanes[0].Cards[0].Todos[0].IsCompleted);
        Assert.Null(loaded.Lanes[0].Cards[0].Todos[0].CompletedAt);
    }

    [Fact]
    public async Task DeleteTodoAsync_RemovesTodo()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Delete");

        await _service.DeleteTodoAsync(board.Id, lane.Id, card.Id, todo.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Lanes[0].Cards[0].Todos);
    }

    [Fact]
    public async Task DeleteTodoAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Keep");

        await _service.DeleteTodoAsync(board.Id, lane.Id, card.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Lanes[0].Cards[0].Todos);
    }

    // ── Board-level Todos ────────────────────────────────────────

    [Fact]
    public async Task AddBoardTodoAsync_AddsTodoToBoard()
    {
        var board = await _service.CreateBoardAsync("Board");

        var todo = await _service.AddBoardTodoAsync(board.Id, "Board task");

        Assert.NotNull(todo);
        Assert.Equal("Board task", todo.Text);
        Assert.Equal(0, todo.Order);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Todos);
    }

    [Fact]
    public async Task AddBoardTodoAsync_SetsIncrementingOrder()
    {
        var board = await _service.CreateBoardAsync("Board");

        var todo1 = await _service.AddBoardTodoAsync(board.Id, "First");
        var todo2 = await _service.AddBoardTodoAsync(board.Id, "Second");

        Assert.Equal(0, todo1.Order);
        Assert.Equal(1, todo2.Order);
    }

    [Fact]
    public async Task AddBoardTodoAsync_ReturnsNull_WhenBoardNotFound()
    {
        var result = await _service.AddBoardTodoAsync(Guid.NewGuid(), "X");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetBoardTodoCompletionAsync_MarksComplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Task");

        await _service.SetBoardTodoCompletionAsync(board.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Todos[0].IsCompleted);
        Assert.NotNull(loaded.Todos[0].CompletedAt);
    }

    [Fact]
    public async Task SetBoardTodoCompletionAsync_MarksIncomplete()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Task");

        await _service.SetBoardTodoCompletionAsync(board.Id, todo.Id, true);
        await _service.SetBoardTodoCompletionAsync(board.Id, todo.Id, false);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.False(loaded!.Todos[0].IsCompleted);
        Assert.Null(loaded.Todos[0].CompletedAt);
    }

    [Fact]
    public async Task SetBoardTodoCompletionAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        await _service.SetBoardTodoCompletionAsync(board.Id, Guid.NewGuid(), true);
    }

    [Fact]
    public async Task DeleteBoardTodoAsync_RemovesTodo()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Delete");

        await _service.DeleteBoardTodoAsync(board.Id, todo.Id);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Empty(loaded!.Todos);
    }

    [Fact]
    public async Task DeleteBoardTodoAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        await _service.AddBoardTodoAsync(board.Id, "Keep");

        await _service.DeleteBoardTodoAsync(board.Id, Guid.NewGuid());

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.Single(loaded!.Todos);
    }

    // ── Today's Todo ─────────────────────────────────────────────

    [Fact]
    public async Task SetTodaysTodoAsync_SetsFlag()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do today");

        await _service.SetTodaysTodoAsync(board.Id, lane.Id, card.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Lanes[0].Cards[0].Todos[0].IsTodaysTodo);
    }

    [Fact]
    public async Task SetTodaysTodoAsync_ClearsFlag()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do today");

        await _service.SetTodaysTodoAsync(board.Id, lane.Id, card.Id, todo.Id, true);
        await _service.SetTodaysTodoAsync(board.Id, lane.Id, card.Id, todo.Id, false);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.False(loaded!.Lanes[0].Cards[0].Todos[0].IsTodaysTodo);
    }

    [Fact]
    public async Task SetTodaysTodoAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");

        await _service.SetTodaysTodoAsync(board.Id, lane.Id, card.Id, Guid.NewGuid(), true);
    }

    [Fact]
    public async Task SetBoardTodaysTodoAsync_SetsFlag()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Board today task");

        await _service.SetBoardTodaysTodoAsync(board.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Todos[0].IsTodaysTodo);
    }

    [Fact]
    public async Task SetBoardTodaysTodoAsync_ClearsFlag()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Board today task");

        await _service.SetBoardTodaysTodoAsync(board.Id, todo.Id, true);
        await _service.SetBoardTodaysTodoAsync(board.Id, todo.Id, false);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.False(loaded!.Todos[0].IsTodaysTodo);
    }

    [Fact]
    public async Task SetBoardTodaysTodoAsync_NoOp_WhenTodoNotFound()
    {
        var board = await _service.CreateBoardAsync("Board");
        await _service.SetBoardTodaysTodoAsync(board.Id, Guid.NewGuid(), true);
    }

    [Fact]
    public async Task SetTodoCompletionAsync_ClearsTodaysTodo_WhenCompleted()
    {
        var board = await _service.CreateBoardAsync("Board");
        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        var todo = await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Do today");

        await _service.SetTodaysTodoAsync(board.Id, lane.Id, card.Id, todo.Id, true);
        await _service.SetTodoCompletionAsync(board.Id, lane.Id, card.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Lanes[0].Cards[0].Todos[0].IsCompleted);
        Assert.False(loaded.Lanes[0].Cards[0].Todos[0].IsTodaysTodo);
    }

    [Fact]
    public async Task SetBoardTodoCompletionAsync_ClearsTodaysTodo_WhenCompleted()
    {
        var board = await _service.CreateBoardAsync("Board");
        var todo = await _service.AddBoardTodoAsync(board.Id, "Board today task");

        await _service.SetBoardTodaysTodoAsync(board.Id, todo.Id, true);
        await _service.SetBoardTodoCompletionAsync(board.Id, todo.Id, true);

        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.Todos[0].IsCompleted);
        Assert.False(loaded.Todos[0].IsTodaysTodo);
    }

    // ── Persistence round-trip ───────────────────────────────────

    [Fact]
    public async Task RoundTrip_FullDataPersistsCorrectly()
    {
        // Create a fully populated board
        var board = await _service.CreateBoardAsync("Full Board", "Column", "Item", "Task");
        var lane = await _service.AddLaneAsync(board.Id, "In Progress");
        var card = await _service.AddCardAsync(board.Id, lane.Id, "Feature", "Build feature X");
        await _service.UpdateCardNotesAsync(board.Id, lane.Id, card.Id, "Important notes");
        await _service.AddCardLinkAsync(board.Id, lane.Id, card.Id, "Spec", "https://spec.example.com");
        await _service.AddTodoAsync(board.Id, lane.Id, card.Id, "Step 1");
        await _service.AddBoardTodoAsync(board.Id, "Board-level task");

        // Create a fresh service pointing at the same file to verify persistence
        var service2 = new TestBoardService();
        // Point it at the same file
        var field = typeof(BoardService).GetField("_filePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(service2, _service.FilePath);

        var loaded = await service2.GetAllBoardsAsync();
        Assert.Single(loaded);

        var b = loaded[0];
        Assert.Equal("Full Board", b.Name);
        Assert.Equal("Column", b.LaneLabel);
        Assert.Single(b.Lanes);
        Assert.Single(b.Lanes[0].Cards);
        Assert.Equal("Important notes", b.Lanes[0].Cards[0].Notes);
        Assert.Single(b.Lanes[0].Cards[0].Links);
        Assert.Equal("https://spec.example.com", b.Lanes[0].Cards[0].Links[0].Url);
        Assert.Single(b.Lanes[0].Cards[0].Todos);
        Assert.Single(b.Todos);
        Assert.Equal("Board-level task", b.Todos[0].Text);

        service2.Dispose();
    }

    // ── LastModified tracking ────────────────────────────────────

    [Fact]
    public async Task Operations_UpdateLastModified()
    {
        var board = await _service.CreateBoardAsync("Board");
        var original = board.LastModified;

        // Small delay to ensure timestamp difference
        await Task.Delay(10);

        var lane = await _service.AddLaneAsync(board.Id, "Lane");
        var loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.LastModified >= original);

        var afterLane = loaded.LastModified;
        await Task.Delay(10);

        await _service.AddCardAsync(board.Id, lane.Id, "Card", "");
        loaded = await _service.GetBoardAsync(board.Id);
        Assert.True(loaded!.LastModified >= afterLane);
    }
}
