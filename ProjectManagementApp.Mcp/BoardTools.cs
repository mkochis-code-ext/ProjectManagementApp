using System.ComponentModel;
using ModelContextProtocol.Server;
using ProjectManagementApp.Models;
using ProjectManagementApp.Services;

namespace ProjectManagementApp.Mcp;

/// <summary>
/// MCP tools exposing the same board operations as the UI. Each tool delegates to
/// <see cref="IBoardService"/>, which (in this process) is the HTTP client that calls
/// the API — so the MCP server never touches the JSON file directly.
/// </summary>
[McpServerToolType]
public class BoardTools
{
    // Boards
    [McpServerTool, Description("List all boards with their lanes, cards, and todos.")]
    public static Task<List<Board>> ListBoards(IBoardService svc) => svc.GetAllBoardsAsync();

    [McpServerTool, Description("Get a single board by its id.")]
    public static Task<Board?> GetBoard(IBoardService svc, Guid boardId) => svc.GetBoardAsync(boardId);

    [McpServerTool, Description("Get the most recently opened board, or the first board if none was marked.")]
    public static Task<Board?> GetLastOpenedBoard(IBoardService svc) => svc.GetLastOpenedBoardAsync();

    // Search
    [McpServerTool, Description("Search boards by name (case-insensitive substring). An empty query returns all boards.")]
    public static Task<List<Board>> SearchBoards(IBoardService svc,
        [Description("Text to match within board names. Empty returns all boards.")] string query)
        => svc.SearchBoardsAsync(query);

    [McpServerTool, Description("Search lanes by name across all boards, or within one board when boardId is supplied. Each result includes the owning board's id and name.")]
    public static Task<List<LaneSearchResult>> SearchLanes(IBoardService svc,
        [Description("Text to match within lane names. Empty returns all lanes in scope.")] string query,
        [Description("Optional board id to limit the search to a single board.")] Guid? boardId = null)
        => svc.SearchLanesAsync(query, boardId);

    [McpServerTool, Description("Search cards by title across all boards, or within one board when boardId is supplied. Each result includes board and lane context so the card can be updated, moved, or deleted.")]
    public static Task<List<CardSearchResult>> SearchCards(IBoardService svc,
        [Description("Text to match within card titles. Empty returns all cards in scope.")] string query,
        [Description("Optional board id to limit the search to a single board.")] Guid? boardId = null)
        => svc.SearchCardsAsync(query, boardId);

    [McpServerTool, Description("Search todos by text across both board-level and card-level todos, optionally scoped to one board. Each result includes board context, and lane/card context for card-level todos.")]
    public static Task<List<TodoSearchResult>> SearchTodos(IBoardService svc,
        [Description("Text to match within todo text. Empty returns all todos in scope.")] string query,
        [Description("Optional board id to limit the search to a single board.")] Guid? boardId = null)
        => svc.SearchTodosAsync(query, boardId);

    [McpServerTool, Description("List all open (incomplete) todos across every board, regardless of project or customer. Includes both board-level and card-level todos, each with their board, lane, and card context.")]
    public static Task<List<TodoSearchResult>> GetOpenTodos(IBoardService svc,
        [Description("Text to match within todo text. Empty returns all open todos.")] string query = "",
        [Description("When true, only return todos flagged as today's todos.")] bool todayOnly = false)
        => svc.GetOpenTodosAsync(query, todayOnly);

    [McpServerTool, Description("Create a new board. Labels customize the terminology for lanes, cards, and todos.")]
    public static Task<Board> CreateBoard(IBoardService svc, string name,
        string laneLabel = "Lane", string cardLabel = "Card", string todoLabel = "Todo")
        => svc.CreateBoardAsync(name, laneLabel, cardLabel, todoLabel);

    [McpServerTool, Description("Update a board's name and labels.")]
    public static async Task<string> UpdateBoard(IBoardService svc, Guid boardId,
        string name, string laneLabel, string cardLabel, string todoLabel)
    {
        await svc.UpdateBoardAsync(boardId, name, laneLabel, cardLabel, todoLabel);
        return "Board updated.";
    }

    [McpServerTool, Description("Set whether a board renders in condensed view.")]
    public static async Task<string> SetBoardViewMode(IBoardService svc, Guid boardId, bool isCondensed)
    {
        await svc.SetBoardViewModeAsync(boardId, isCondensed);
        return "View mode updated.";
    }

    [McpServerTool, Description("Mark a board as the last opened board.")]
    public static async Task<string> SetLastOpenedBoard(IBoardService svc, Guid boardId)
    {
        await svc.SetLastOpenedBoardAsync(boardId);
        return "Last opened board set.";
    }

    [McpServerTool, Description("Delete a board and all its contents.")]
    public static async Task<string> DeleteBoard(IBoardService svc, Guid boardId)
    {
        await svc.DeleteBoardAsync(boardId);
        return "Board deleted.";
    }

    // Lanes
    [McpServerTool, Description("Add a lane (column) to a board.")]
    public static Task<Lane> AddLane(IBoardService svc, Guid boardId, string name) => svc.AddLaneAsync(boardId, name);

    [McpServerTool, Description("Rename a lane.")]
    public static async Task<string> UpdateLane(IBoardService svc, Guid boardId, Guid laneId, string name)
    {
        await svc.UpdateLaneAsync(boardId, laneId, name);
        return "Lane updated.";
    }

    [McpServerTool, Description("Archive or unarchive a lane. Archived lanes are hidden on the board unless completed items are shown.")]
    public static async Task<string> SetLaneArchived(IBoardService svc, Guid boardId, Guid laneId, bool isArchived)
    {
        await svc.SetLaneArchivedAsync(boardId, laneId, isArchived);
        return isArchived ? "Lane archived." : "Lane unarchived.";
    }

    [McpServerTool, Description("Move a lane one position in display order. Use direction -1 to move earlier, 1 to move later.")]
    public static async Task<string> MoveLane(IBoardService svc, Guid boardId, Guid laneId, int direction)
    {
        await svc.MoveLaneAsync(boardId, laneId, direction);
        return "Lane moved.";
    }

    [McpServerTool, Description("Delete a lane and its cards.")]
    public static async Task<string> DeleteLane(IBoardService svc, Guid boardId, Guid laneId)
    {
        await svc.DeleteLaneAsync(boardId, laneId);
        return "Lane deleted.";
    }

    // Cards
    [McpServerTool, Description("Add a card to a lane.")]
    public static Task<Card> AddCard(IBoardService svc, Guid boardId, Guid laneId, string title, string description)
        => svc.AddCardAsync(boardId, laneId, title, description);

    [McpServerTool, Description("Update a card's title and description.")]
    public static async Task<string> UpdateCard(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, string title, string description)
    {
        await svc.UpdateCardAsync(boardId, laneId, cardId, title, description);
        return "Card updated.";
    }

    [McpServerTool, Description("Mark a card complete or incomplete.")]
    public static async Task<string> SetCardCompletion(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, bool isCompleted)
    {
        await svc.SetCardCompletionAsync(boardId, laneId, cardId, isCompleted);
        return "Card completion updated.";
    }

    [McpServerTool, Description("Move a card from one lane to another.")]
    public static async Task<string> MoveCard(IBoardService svc, Guid boardId, Guid fromLaneId, Guid toLaneId, Guid cardId)
    {
        await svc.MoveCardAsync(boardId, fromLaneId, toLaneId, cardId);
        return "Card moved.";
    }

    [McpServerTool, Description("Delete a card.")]
    public static async Task<string> DeleteCard(IBoardService svc, Guid boardId, Guid laneId, Guid cardId)
    {
        await svc.DeleteCardAsync(boardId, laneId, cardId);
        return "Card deleted.";
    }

    [McpServerTool, Description("Update the free-form notes on a card.")]
    public static async Task<string> UpdateCardNotes(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, string notes)
    {
        await svc.UpdateCardNotesAsync(boardId, laneId, cardId, notes);
        return "Card notes updated.";
    }

    [McpServerTool, Description("Add a link to a card.")]
    public static Task<CardLink> AddCardLink(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, string title, string url)
        => svc.AddCardLinkAsync(boardId, laneId, cardId, title, url);

    [McpServerTool, Description("Delete a link from a card.")]
    public static async Task<string> DeleteCardLink(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid linkId)
    {
        await svc.DeleteCardLinkAsync(boardId, laneId, cardId, linkId);
        return "Card link deleted.";
    }

    [McpServerTool, Description("Add a contact to a card.")]
    public static Task<CardContact> AddCardContact(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, string name, string email)
        => svc.AddCardContactAsync(boardId, laneId, cardId, name, email);

    [McpServerTool, Description("Delete a contact from a card.")]
    public static async Task<string> DeleteCardContact(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid contactId)
    {
        await svc.DeleteCardContactAsync(boardId, laneId, cardId, contactId);
        return "Card contact deleted.";
    }

    // Card-level todos
    [McpServerTool, Description("Add a todo to a card.")]
    public static Task<TodoItem> AddTodo(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, string text,
        bool isTodaysTodo = false, string notes = "")
        => svc.AddTodoAsync(boardId, laneId, cardId, text, isTodaysTodo, notes);

    [McpServerTool, Description("Mark a card todo complete or incomplete.")]
    public static async Task<string> SetTodoCompletion(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isCompleted)
    {
        await svc.SetTodoCompletionAsync(boardId, laneId, cardId, todoId, isCompleted);
        return "Todo completion updated.";
    }

    [McpServerTool, Description("Delete a card todo.")]
    public static async Task<string> DeleteTodo(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId)
    {
        await svc.DeleteTodoAsync(boardId, laneId, cardId, todoId);
        return "Todo deleted.";
    }

    [McpServerTool, Description("Flag or unflag a card todo as a 'today' todo.")]
    public static async Task<string> SetTodaysTodo(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isTodaysTodo)
    {
        await svc.SetTodaysTodoAsync(boardId, laneId, cardId, todoId, isTodaysTodo);
        return "Today's todo flag updated.";
    }

    [McpServerTool, Description("Update the notes on a card todo.")]
    public static async Task<string> UpdateTodoNotes(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, string notes)
    {
        await svc.UpdateTodoNotesAsync(boardId, laneId, cardId, todoId, notes);
        return "Todo notes updated.";
    }

    [McpServerTool, Description("Update the text of a card todo.")]
    public static async Task<string> UpdateTodoText(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, string text)
    {
        await svc.UpdateTodoTextAsync(boardId, laneId, cardId, todoId, text);
        return "Todo text updated.";
    }

    [McpServerTool, Description("Add a link to a card todo.")]
    public static Task<TodoLink> AddTodoLink(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, string title, string url)
        => svc.AddTodoLinkAsync(boardId, laneId, cardId, todoId, title, url);

    [McpServerTool, Description("Delete a link from a card todo.")]
    public static async Task<string> DeleteTodoLink(IBoardService svc, Guid boardId, Guid laneId, Guid cardId, Guid todoId, Guid linkId)
    {
        await svc.DeleteTodoLinkAsync(boardId, laneId, cardId, todoId, linkId);
        return "Todo link deleted.";
    }

    // Board-level todos
    [McpServerTool, Description("Add a board-level todo.")]
    public static Task<TodoItem> AddBoardTodo(IBoardService svc, Guid boardId, string text, bool isTodaysTodo = false, string notes = "")
        => svc.AddBoardTodoAsync(boardId, text, isTodaysTodo, notes);

    [McpServerTool, Description("Mark a board-level todo complete or incomplete.")]
    public static async Task<string> SetBoardTodoCompletion(IBoardService svc, Guid boardId, Guid todoId, bool isCompleted)
    {
        await svc.SetBoardTodoCompletionAsync(boardId, todoId, isCompleted);
        return "Board todo completion updated.";
    }

    [McpServerTool, Description("Delete a board-level todo.")]
    public static async Task<string> DeleteBoardTodo(IBoardService svc, Guid boardId, Guid todoId)
    {
        await svc.DeleteBoardTodoAsync(boardId, todoId);
        return "Board todo deleted.";
    }

    [McpServerTool, Description("Flag or unflag a board-level todo as a 'today' todo.")]
    public static async Task<string> SetBoardTodaysTodo(IBoardService svc, Guid boardId, Guid todoId, bool isTodaysTodo)
    {
        await svc.SetBoardTodaysTodoAsync(boardId, todoId, isTodaysTodo);
        return "Board today's todo flag updated.";
    }

    [McpServerTool, Description("Update the notes on a board-level todo.")]
    public static async Task<string> UpdateBoardTodoNotes(IBoardService svc, Guid boardId, Guid todoId, string notes)
    {
        await svc.UpdateBoardTodoNotesAsync(boardId, todoId, notes);
        return "Board todo notes updated.";
    }

    [McpServerTool, Description("Update the text of a board-level todo.")]
    public static async Task<string> UpdateBoardTodoText(IBoardService svc, Guid boardId, Guid todoId, string text)
    {
        await svc.UpdateBoardTodoTextAsync(boardId, todoId, text);
        return "Board todo text updated.";
    }

    [McpServerTool, Description("Add a link to a board-level todo.")]
    public static Task<TodoLink> AddBoardTodoLink(IBoardService svc, Guid boardId, Guid todoId, string title, string url)
        => svc.AddBoardTodoLinkAsync(boardId, todoId, title, url);

    [McpServerTool, Description("Delete a link from a board-level todo.")]
    public static async Task<string> DeleteBoardTodoLink(IBoardService svc, Guid boardId, Guid todoId, Guid linkId)
    {
        await svc.DeleteBoardTodoLinkAsync(boardId, todoId, linkId);
        return "Board todo link deleted.";
    }
}
