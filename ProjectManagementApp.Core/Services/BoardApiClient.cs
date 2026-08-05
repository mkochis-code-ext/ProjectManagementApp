using System.Net;
using System.Net.Http.Json;
using ProjectManagementApp.Models;

namespace ProjectManagementApp.Services;

/// <summary>
/// HTTP-based <see cref="IBoardService"/> implementation. All operations are
/// delegated to the API process, which is the sole owner of the JSON data file.
/// Method signatures mirror <see cref="BoardService"/> so UI call sites are unchanged.
/// </summary>
public class BoardApiClient : IBoardService
{
    private readonly HttpClient _http;

    public BoardApiClient(HttpClient http) => _http = http;

    // Boards
    public async Task<List<Board>> GetAllBoardsAsync() =>
        await _http.GetFromJsonAsync<List<Board>>("api/boards") ?? new List<Board>();

    public async Task<Board?> GetBoardAsync(Guid boardId)
    {
        var resp = await _http.GetAsync($"api/boards/{boardId}");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Board>();
    }

    public async Task<Board?> GetLastOpenedBoardAsync()
    {
        var resp = await _http.GetAsync("api/boards/last-opened");
        if (resp.StatusCode == HttpStatusCode.NoContent) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Board>();
    }

    // Search
    public async Task<List<Board>> SearchBoardsAsync(string query) =>
        await _http.GetFromJsonAsync<List<Board>>($"api/boards?search={Uri.EscapeDataString(query ?? string.Empty)}") ?? new List<Board>();

    public async Task<List<LaneSearchResult>> SearchLanesAsync(string query, Guid? boardId = null) =>
        await _http.GetFromJsonAsync<List<LaneSearchResult>>(SearchUrl("lanes", query, boardId)) ?? new List<LaneSearchResult>();

    public async Task<List<CardSearchResult>> SearchCardsAsync(string query, Guid? boardId = null) =>
        await _http.GetFromJsonAsync<List<CardSearchResult>>(SearchUrl("cards", query, boardId)) ?? new List<CardSearchResult>();

    public async Task<List<TodoSearchResult>> SearchTodosAsync(string query, Guid? boardId = null) =>
        await _http.GetFromJsonAsync<List<TodoSearchResult>>(SearchUrl("todos", query, boardId)) ?? new List<TodoSearchResult>();

    public async Task<List<TodoSearchResult>> GetOpenTodosAsync(string query, bool todayOnly = false)
    {
        var url = $"api/boards/open-todos?q={Uri.EscapeDataString(query ?? string.Empty)}&today={todayOnly.ToString().ToLowerInvariant()}";
        return await _http.GetFromJsonAsync<List<TodoSearchResult>>(url) ?? new List<TodoSearchResult>();
    }

    private static string SearchUrl(string kind, string query, Guid? boardId)
    {
        var url = $"api/boards/search/{kind}?q={Uri.EscapeDataString(query ?? string.Empty)}";
        if (boardId is not null) url += $"&boardId={boardId.Value}";
        return url;
    }

    public Task SetLastOpenedBoardAsync(Guid boardId) =>
        Post($"api/boards/{boardId}/last-opened", null);

    public Task<Board> CreateBoardAsync(string name, string laneLabel = "Lane", string cardLabel = "Card", string todoLabel = "Todo") =>
        Post<Board>("api/boards", new { name, laneLabel, cardLabel, todoLabel });

    public Task UpdateBoardAsync(Guid boardId, string name, string laneLabel, string cardLabel, string todoLabel) =>
        Put($"api/boards/{boardId}", new { name, laneLabel, cardLabel, todoLabel });

    public Task SetBoardViewModeAsync(Guid boardId, bool isCondensed) =>
        Post($"api/boards/{boardId}/view-mode", new { isCondensed });

    public Task DeleteBoardAsync(Guid boardId) =>
        Delete($"api/boards/{boardId}");

    // Lanes
    public Task<Lane> AddLaneAsync(Guid boardId, string name) =>
        Post<Lane>($"api/boards/{boardId}/lanes", new { name });

    public Task UpdateLaneAsync(Guid boardId, Guid laneId, string name) =>
        Put($"api/boards/{boardId}/lanes/{laneId}", new { name });

    public Task SetLaneArchivedAsync(Guid boardId, Guid laneId, bool isArchived) =>
        Post($"api/boards/{boardId}/lanes/{laneId}/archive", new { isArchived });

    public Task MoveLaneAsync(Guid boardId, Guid laneId, int direction) =>
        Post($"api/boards/{boardId}/lanes/{laneId}/move", new { direction });

    public Task DeleteLaneAsync(Guid boardId, Guid laneId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}");

    // Cards
    public Task<Card> AddCardAsync(Guid boardId, Guid laneId, string title, string description) =>
        Post<Card>($"api/boards/{boardId}/lanes/{laneId}/cards", new { title, description });

    public Task UpdateCardAsync(Guid boardId, Guid laneId, Guid cardId, string title, string description) =>
        Put($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}", new { title, description });

    public Task SetCardCompletionAsync(Guid boardId, Guid laneId, Guid cardId, bool isCompleted) =>
        Post($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/completion", new { isCompleted });

    public Task MoveCardAsync(Guid boardId, Guid fromLaneId, Guid toLaneId, Guid cardId) =>
        Post($"api/boards/{boardId}/cards/{cardId}/move", new { fromLaneId, toLaneId });

    public Task DeleteCardAsync(Guid boardId, Guid laneId, Guid cardId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}");

    public Task UpdateCardNotesAsync(Guid boardId, Guid laneId, Guid cardId, string notes) =>
        Put($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/notes", new { notes });

    public Task<CardLink> AddCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, string title, string url) =>
        Post<CardLink>($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/links", new { title, url });

    public Task DeleteCardLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid linkId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/links/{linkId}");

    public Task<CardContact> AddCardContactAsync(Guid boardId, Guid laneId, Guid cardId, string name, string email) =>
        Post<CardContact>($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/contacts", new { name, email });

    public Task DeleteCardContactAsync(Guid boardId, Guid laneId, Guid cardId, Guid contactId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/contacts/{contactId}");

    // Card-level todos
    public Task<TodoItem> AddTodoAsync(Guid boardId, Guid laneId, Guid cardId, string text, bool isTodaysTodo = false, string notes = "") =>
        Post<TodoItem>($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos", new { text, isTodaysTodo, notes });

    public Task SetTodoCompletionAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isCompleted) =>
        Post($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/completion", new { isCompleted });

    public Task DeleteTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}");

    public Task SetTodaysTodoAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, bool isTodaysTodo) =>
        Post($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/todays", new { isTodaysTodo });

    public Task UpdateTodoNotesAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string notes) =>
        Put($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/notes", new { notes });

    public Task UpdateTodoTextAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string text) =>
        Put($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/text", new { text });

    public Task<TodoLink> AddTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, string title, string url) =>
        Post<TodoLink>($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/links", new { title, url });

    public Task DeleteTodoLinkAsync(Guid boardId, Guid laneId, Guid cardId, Guid todoId, Guid linkId) =>
        Delete($"api/boards/{boardId}/lanes/{laneId}/cards/{cardId}/todos/{todoId}/links/{linkId}");

    // Board-level todos
    public Task<TodoItem> AddBoardTodoAsync(Guid boardId, string text, bool isTodaysTodo = false, string notes = "") =>
        Post<TodoItem>($"api/boards/{boardId}/todos", new { text, isTodaysTodo, notes });

    public Task SetBoardTodoCompletionAsync(Guid boardId, Guid todoId, bool isCompleted) =>
        Post($"api/boards/{boardId}/todos/{todoId}/completion", new { isCompleted });

    public Task DeleteBoardTodoAsync(Guid boardId, Guid todoId) =>
        Delete($"api/boards/{boardId}/todos/{todoId}");

    public Task SetBoardTodaysTodoAsync(Guid boardId, Guid todoId, bool isTodaysTodo) =>
        Post($"api/boards/{boardId}/todos/{todoId}/todays", new { isTodaysTodo });

    public Task UpdateBoardTodoNotesAsync(Guid boardId, Guid todoId, string notes) =>
        Put($"api/boards/{boardId}/todos/{todoId}/notes", new { notes });

    public Task UpdateBoardTodoTextAsync(Guid boardId, Guid todoId, string text) =>
        Put($"api/boards/{boardId}/todos/{todoId}/text", new { text });

    public Task<TodoLink> AddBoardTodoLinkAsync(Guid boardId, Guid todoId, string title, string url) =>
        Post<TodoLink>($"api/boards/{boardId}/todos/{todoId}/links", new { title, url });

    public Task DeleteBoardTodoLinkAsync(Guid boardId, Guid todoId, Guid linkId) =>
        Delete($"api/boards/{boardId}/todos/{todoId}/links/{linkId}");

    // Helpers
    private async Task Post(string url, object? body)
    {
        var resp = await _http.PostAsJsonAsync(url, body ?? new { });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var resp = await _http.PostAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task Put(string url, object body)
    {
        var resp = await _http.PutAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
    }

    private async Task Delete(string url)
    {
        var resp = await _http.DeleteAsync(url);
        resp.EnsureSuccessStatusCode();
    }
}
