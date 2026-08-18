using ProjectManagementApp.Models;

namespace ProjectManagementApp.Services;

/// <summary>
/// The board operations contract shared by the in-process <see cref="BoardService"/>
/// (used by the API, which owns the JSON file) and the HTTP-based client used by the
/// UI and MCP server. Method signatures mirror <see cref="BoardService"/> so callers
/// are agnostic to whether data access is local or remote.
/// </summary>
public interface IBoardService
{
    Task<List<Board>> GetAllBoardsAsync();
    Task<Board?> GetBoardAsync(Guid boardId);
    Task<Board?> GetLastOpenedBoardAsync();

    // Name-based search (case-insensitive substring; empty query matches everything).
    // boardId optionally scopes nested-item searches to a single board.
    Task<List<Board>> SearchBoardsAsync(string query);
    Task<List<LaneSearchResult>> SearchLanesAsync(string query, Guid? boardId = null);
    Task<List<CardSearchResult>> SearchCardsAsync(string query, Guid? boardId = null);
    Task<List<TodoSearchResult>> SearchTodosAsync(string query, Guid? boardId = null);

    // All open (incomplete) todos across every board, both board-level and card-level.
    // todayOnly limits results to todos flagged for today; query filters by todo text.
    Task<List<TodoSearchResult>> GetOpenTodosAsync(string query, bool todayOnly = false);

    Task SetLastOpenedBoardAsync(Guid boardId);
    Task<Board> CreateBoardAsync(string name, string laneLabel = "Lane", string cardLabel = "Card", string todoLabel = "Todo");
    Task UpdateBoardAsync(Guid boardId, string name, string laneLabel, string cardLabel, string todoLabel);
    Task SetBoardViewModeAsync(Guid boardId, bool isCondensed);
    Task DeleteBoardAsync(Guid boardId);

    Task<Lane> AddLaneAsync(Guid boardId, string name);
    Task UpdateLaneAsync(Guid boardId, Guid laneId, string name);
    Task SetLaneArchivedAsync(Guid boardId, Guid laneId, bool isArchived);
    // Moves a lane one position in display order; direction is negative (earlier) or positive (later).
    Task MoveLaneAsync(Guid boardId, Guid laneId, int direction);
    Task DeleteLaneAsync(Guid boardId, Guid laneId);

    Task<Card> AddCardAsync(Guid boardId, Guid laneId, string title, string description);
    Task UpdateCardAsync(Guid boardId, Guid laneId, Guid cardId, string title, string description);
    Task SetCardCompletionAsync(Guid boardId, Guid laneId, Guid cardId, bool isCompleted);
    Task MoveCardAsync(Guid boardId, Guid fromLaneId, Guid toLaneId, Guid cardId);
    Task DeleteCardAsync(Guid boardId, Guid laneId, Guid cardId);
    Task UpdateCardNotesAsync(Guid boardId, Guid laneId, Guid cardId, string notes);
    Task<CardLink> AddCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, string title, string url);
    Task DeleteCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid linkId);
    Task<CardContact> AddCardContactAsync(Guid boardId, Guid laneId, Guid cardId, string name, string email);
    Task DeleteCardContactAsync(Guid boardId, Guid laneId, Guid cardId, Guid contactId);

    Task<TodoItem> AddTodoAsync(Guid boardId, Guid laneId, Guid cardId, string text, bool isTodaysTodo = false, string notes = "", DateTime? dueDate = null);
    Task SetTodoCompletionAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isCompleted);
    Task DeleteTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId);
    Task SetTodaysTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isTodaysTodo);
    Task UpdateTodoNotesAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string notes);
    Task UpdateTodoDueDateAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, DateTime? dueDate);
    Task UpdateTodoTextAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string text);
    Task<TodoLink> AddTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string title, string url);
    Task DeleteTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, Guid linkId);

    Task<TodoItem> AddBoardTodoAsync(Guid boardId, string text, bool isTodaysTodo = false, string notes = "", DateTime? dueDate = null);
    Task SetBoardTodoCompletionAsync(Guid boardId, Guid todoId, bool isCompleted);
    Task DeleteBoardTodoAsync(Guid boardId, Guid todoId);
    Task SetBoardTodaysTodoAsync(Guid boardId, Guid todoId, bool isTodaysTodo);
    Task UpdateBoardTodoNotesAsync(Guid boardId, Guid todoId, string notes);
    Task UpdateBoardTodoDueDateAsync(Guid boardId, Guid todoId, DateTime? dueDate);
    Task UpdateBoardTodoTextAsync(Guid boardId, Guid todoId, string text);
    Task<TodoLink> AddBoardTodoLinkAsync(Guid boardId, Guid todoId, string title, string url);
    Task DeleteBoardTodoLinkAsync(Guid boardId, Guid todoId, Guid linkId);
}
