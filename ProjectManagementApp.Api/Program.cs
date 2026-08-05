using ProjectManagementApp.Models;
using ProjectManagementApp.Services;
using ProjectManagementApp.Api;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// The API process is the SOLE owner of BoardCollection.json.
// A singleton BoardService keeps one in-memory collection shared by all requests.
builder.Services.AddSingleton<BoardService>();

builder.Services.AddHealthChecks()
    .AddCheck<BoardFileHealthCheck>("data-file");

var app = builder.Build();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteJson
});

var boards = app.MapGroup("/api/boards");

// Board collection
boards.MapGet("/", async (BoardService svc, string? search) =>
    Results.Ok(await svc.SearchBoardsAsync(search ?? string.Empty)));

// Search nested items by name (case-insensitive substring; optional boardId scope)
boards.MapGet("/search/lanes", async (BoardService svc, string? q, Guid? boardId) =>
    Results.Ok(await svc.SearchLanesAsync(q ?? string.Empty, boardId)));
boards.MapGet("/search/cards", async (BoardService svc, string? q, Guid? boardId) =>
    Results.Ok(await svc.SearchCardsAsync(q ?? string.Empty, boardId)));
boards.MapGet("/search/todos", async (BoardService svc, string? q, Guid? boardId) =>
    Results.Ok(await svc.SearchTodosAsync(q ?? string.Empty, boardId)));

// All open (incomplete) todos across every board, optionally limited to today's todos
// and/or filtered by a text query.
boards.MapGet("/open-todos", async (BoardService svc, string? q, bool? today) =>
    Results.Ok(await svc.GetOpenTodosAsync(q ?? string.Empty, today ?? false)));

boards.MapGet("/last-opened", async (BoardService svc) =>
{
    var board = await svc.GetLastOpenedBoardAsync();
    return board is null ? Results.NoContent() : Results.Ok(board);
});
boards.MapGet("/{boardId:guid}", async (Guid boardId, BoardService svc) =>
{
    var board = await svc.GetBoardAsync(boardId);
    return board is null ? Results.NotFound() : Results.Ok(board);
});
boards.MapPost("/", async (CreateBoardRequest req, BoardService svc) =>
{
    var board = await svc.CreateBoardAsync(req.Name, req.LaneLabel, req.CardLabel, req.TodoLabel);
    return Results.Created($"/api/boards/{board.Id}", board);
});
boards.MapPut("/{boardId:guid}", async (Guid boardId, UpdateBoardRequest req, BoardService svc) =>
{
    await svc.UpdateBoardAsync(boardId, req.Name, req.LaneLabel, req.CardLabel, req.TodoLabel);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/view-mode", async (Guid boardId, ViewModeRequest req, BoardService svc) =>
{
    await svc.SetBoardViewModeAsync(boardId, req.IsCondensed);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/last-opened", async (Guid boardId, BoardService svc) =>
{
    await svc.SetLastOpenedBoardAsync(boardId);
    return Results.NoContent();
});
boards.MapDelete("/{boardId:guid}", async (Guid boardId, BoardService svc) =>
{
    await svc.DeleteBoardAsync(boardId);
    return Results.NoContent();
});

// Lanes
boards.MapPost("/{boardId:guid}/lanes", async (Guid boardId, NameRequest req, BoardService svc) =>
    Results.Ok(await svc.AddLaneAsync(boardId, req.Name)));
boards.MapPut("/{boardId:guid}/lanes/{laneId:guid}", async (Guid boardId, Guid laneId, NameRequest req, BoardService svc) =>
{
    await svc.UpdateLaneAsync(boardId, laneId, req.Name);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/archive", async (Guid boardId, Guid laneId, ArchiveRequest req, BoardService svc) =>
{
    await svc.SetLaneArchivedAsync(boardId, laneId, req.IsArchived);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/move", async (Guid boardId, Guid laneId, MoveLaneRequest req, BoardService svc) =>
{
    await svc.MoveLaneAsync(boardId, laneId, req.Direction);
    return Results.NoContent();
});
boards.MapDelete("/{boardId:guid}/lanes/{laneId:guid}", async (Guid boardId, Guid laneId, BoardService svc) =>
{
    await svc.DeleteLaneAsync(boardId, laneId);
    return Results.NoContent();
});

// Cards
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/cards", async (Guid boardId, Guid laneId, CardRequest req, BoardService svc) =>
    Results.Ok(await svc.AddCardAsync(boardId, laneId, req.Title, req.Description)));
boards.MapPut("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}", async (Guid boardId, Guid laneId, Guid cardId, CardRequest req, BoardService svc) =>
{
    await svc.UpdateCardAsync(boardId, laneId, cardId, req.Title, req.Description);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/completion", async (Guid boardId, Guid laneId, Guid cardId, CompletionRequest req, BoardService svc) =>
{
    await svc.SetCardCompletionAsync(boardId, laneId, cardId, req.IsCompleted);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/cards/{cardId:guid}/move", async (Guid boardId, Guid cardId, MoveCardRequest req, BoardService svc) =>
{
    await svc.MoveCardAsync(boardId, req.FromLaneId, req.ToLaneId, cardId);
    return Results.NoContent();
});
boards.MapDelete("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}", async (Guid boardId, Guid laneId, Guid cardId, BoardService svc) =>
{
    await svc.DeleteCardAsync(boardId, laneId, cardId);
    return Results.NoContent();
});
boards.MapPut("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/notes", async (Guid boardId, Guid laneId, Guid cardId, NotesRequest req, BoardService svc) =>
{
    await svc.UpdateCardNotesAsync(boardId, laneId, cardId, req.Notes);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/links", async (Guid boardId, Guid laneId, Guid cardId, LinkRequest req, BoardService svc) =>
    Results.Ok(await svc.AddCardLinkAsync(boardId, laneId, cardId, req.Title, req.Url)));
boards.MapDelete("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/links/{linkId:guid}", async (Guid boardId, Guid laneId, Guid cardId, Guid linkId, BoardService svc) =>
{
    await svc.DeleteCardLinkAsync(boardId, laneId, cardId, linkId);
    return Results.NoContent();
});
boards.MapPost("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/contacts", async (Guid boardId, Guid laneId, Guid cardId, ContactRequest req, BoardService svc) =>
    Results.Ok(await svc.AddCardContactAsync(boardId, laneId, cardId, req.Name, req.Email)));
boards.MapDelete("/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/contacts/{contactId:guid}", async (Guid boardId, Guid laneId, Guid cardId, Guid contactId, BoardService svc) =>
{
    await svc.DeleteCardContactAsync(boardId, laneId, cardId, contactId);
    return Results.NoContent();
});

// Card-level todos
var cardTodos = "/{boardId:guid}/lanes/{laneId:guid}/cards/{cardId:guid}/todos";
boards.MapPost(cardTodos, async (Guid boardId, Guid laneId, Guid cardId, TodoRequest req, BoardService svc) =>
    Results.Ok(await svc.AddTodoAsync(boardId, laneId, cardId, req.Text, req.IsTodaysTodo, req.Notes)));
boards.MapPut(cardTodos + "/{todoId:guid}/text", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, TextRequest req, BoardService svc) =>
{
    await svc.UpdateTodoTextAsync(boardId, laneId, cardId, todoId, req.Text);
    return Results.NoContent();
});
boards.MapPost(cardTodos + "/{todoId:guid}/completion", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, CompletionRequest req, BoardService svc) =>
{
    await svc.SetTodoCompletionAsync(boardId, laneId, cardId, todoId, req.IsCompleted);
    return Results.NoContent();
});
boards.MapDelete(cardTodos + "/{todoId:guid}", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, BoardService svc) =>
{
    await svc.DeleteTodoAsync(boardId, laneId, cardId, todoId);
    return Results.NoContent();
});
boards.MapPost(cardTodos + "/{todoId:guid}/todays", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, TodaysTodoRequest req, BoardService svc) =>
{
    await svc.SetTodaysTodoAsync(boardId, laneId, cardId, todoId, req.IsTodaysTodo);
    return Results.NoContent();
});
boards.MapPut(cardTodos + "/{todoId:guid}/notes", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, NotesRequest req, BoardService svc) =>
{
    await svc.UpdateTodoNotesAsync(boardId, laneId, cardId, todoId, req.Notes);
    return Results.NoContent();
});
boards.MapPost(cardTodos + "/{todoId:guid}/links", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, LinkRequest req, BoardService svc) =>
    Results.Ok(await svc.AddTodoLinkAsync(boardId, laneId, cardId, todoId, req.Title, req.Url)));
boards.MapDelete(cardTodos + "/{todoId:guid}/links/{linkId:guid}", async (Guid boardId, Guid laneId, Guid cardId, Guid todoId, Guid linkId, BoardService svc) =>
{
    await svc.DeleteTodoLinkAsync(boardId, laneId, cardId, todoId, linkId);
    return Results.NoContent();
});

// Board-level todos
var boardTodos = "/{boardId:guid}/todos";
boards.MapPost(boardTodos, async (Guid boardId, TodoRequest req, BoardService svc) =>
    Results.Ok(await svc.AddBoardTodoAsync(boardId, req.Text, req.IsTodaysTodo, req.Notes)));
boards.MapPost(boardTodos + "/{todoId:guid}/completion", async (Guid boardId, Guid todoId, CompletionRequest req, BoardService svc) =>
{
    await svc.SetBoardTodoCompletionAsync(boardId, todoId, req.IsCompleted);
    return Results.NoContent();
});
boards.MapDelete(boardTodos + "/{todoId:guid}", async (Guid boardId, Guid todoId, BoardService svc) =>
{
    await svc.DeleteBoardTodoAsync(boardId, todoId);
    return Results.NoContent();
});
boards.MapPost(boardTodos + "/{todoId:guid}/todays", async (Guid boardId, Guid todoId, TodaysTodoRequest req, BoardService svc) =>
{
    await svc.SetBoardTodaysTodoAsync(boardId, todoId, req.IsTodaysTodo);
    return Results.NoContent();
});
boards.MapPut(boardTodos + "/{todoId:guid}/notes", async (Guid boardId, Guid todoId, NotesRequest req, BoardService svc) =>
{
    await svc.UpdateBoardTodoNotesAsync(boardId, todoId, req.Notes);
    return Results.NoContent();
});
boards.MapPut(boardTodos + "/{todoId:guid}/text", async (Guid boardId, Guid todoId, TextRequest req, BoardService svc) =>
{
    await svc.UpdateBoardTodoTextAsync(boardId, todoId, req.Text);
    return Results.NoContent();
});
boards.MapPost(boardTodos + "/{todoId:guid}/links", async (Guid boardId, Guid todoId, LinkRequest req, BoardService svc) =>
    Results.Ok(await svc.AddBoardTodoLinkAsync(boardId, todoId, req.Title, req.Url)));
boards.MapDelete(boardTodos + "/{todoId:guid}/links/{linkId:guid}", async (Guid boardId, Guid todoId, Guid linkId, BoardService svc) =>
{
    await svc.DeleteBoardTodoLinkAsync(boardId, todoId, linkId);
    return Results.NoContent();
});

app.Run();

// Request DTOs
record CreateBoardRequest(string Name, string LaneLabel = "Lane", string CardLabel = "Card", string TodoLabel = "Todo");
record UpdateBoardRequest(string Name, string LaneLabel, string CardLabel, string TodoLabel);
record ViewModeRequest(bool IsCondensed);
record NameRequest(string Name);
record ArchiveRequest(bool IsArchived);
record MoveLaneRequest(int Direction);
record CardRequest(string Title, string Description);
record CompletionRequest(bool IsCompleted);
record MoveCardRequest(Guid FromLaneId, Guid ToLaneId);
record NotesRequest(string Notes);
record TextRequest(string Text);
record LinkRequest(string Title, string Url);
record ContactRequest(string Name, string Email);
record TodoRequest(string Text, bool IsTodaysTodo = false, string Notes = "");
record TodaysTodoRequest(bool IsTodaysTodo);
