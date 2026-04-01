using ProjectManagementApp.Models;

namespace ProjectManagementApp.Tests;

public class KanbanModelsTests
{
    [Fact]
    public void BoardCollection_DefaultValues()
    {
        var collection = new BoardCollection();

        Assert.NotNull(collection.Boards);
        Assert.Empty(collection.Boards);
        Assert.Null(collection.LastOpenedBoardId);
    }

    [Fact]
    public void Board_DefaultValues()
    {
        var board = new Board();

        Assert.NotEqual(Guid.Empty, board.Id);
        Assert.Equal("New Board", board.Name);
        Assert.Equal("Lane", board.LaneLabel);
        Assert.Equal("Card", board.CardLabel);
        Assert.Equal("Todo", board.TodoLabel);
        Assert.NotNull(board.Lanes);
        Assert.Empty(board.Lanes);
        Assert.NotNull(board.Todos);
        Assert.Empty(board.Todos);
        Assert.True(board.CreatedAt <= DateTime.UtcNow);
        Assert.True(board.LastModified <= DateTime.UtcNow);
    }

    [Fact]
    public void Board_UniqueIds()
    {
        var board1 = new Board();
        var board2 = new Board();

        Assert.NotEqual(board1.Id, board2.Id);
    }

    [Fact]
    public void Lane_DefaultValues()
    {
        var lane = new Lane();

        Assert.NotEqual(Guid.Empty, lane.Id);
        Assert.Equal(string.Empty, lane.Name);
        Assert.Equal(0, lane.Order);
        Assert.True(lane.CreatedAt <= DateTime.UtcNow);
        Assert.NotNull(lane.Cards);
        Assert.Empty(lane.Cards);
    }

    [Fact]
    public void Lane_UniqueIds()
    {
        var lane1 = new Lane();
        var lane2 = new Lane();

        Assert.NotEqual(lane1.Id, lane2.Id);
    }

    [Fact]
    public void Card_DefaultValues()
    {
        var card = new Card();

        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal(string.Empty, card.Title);
        Assert.Equal(string.Empty, card.Description);
        Assert.Equal(string.Empty, card.Notes);
        Assert.False(card.IsCompleted);
        Assert.True(card.CreatedAt <= DateTime.UtcNow);
        Assert.Null(card.CompletedAt);
        Assert.Equal(0, card.Order);
        Assert.NotNull(card.Todos);
        Assert.Empty(card.Todos);
        Assert.NotNull(card.Links);
        Assert.Empty(card.Links);
    }

    [Fact]
    public void Card_UniqueIds()
    {
        var card1 = new Card();
        var card2 = new Card();

        Assert.NotEqual(card1.Id, card2.Id);
    }

    [Fact]
    public void Card_SetCompletedProperties()
    {
        var card = new Card
        {
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow
        };

        Assert.True(card.IsCompleted);
        Assert.NotNull(card.CompletedAt);
    }

    [Fact]
    public void CardLink_DefaultValues()
    {
        var link = new CardLink();

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal(string.Empty, link.Title);
        Assert.Equal(string.Empty, link.Url);
        Assert.True(link.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void CardLink_UniqueIds()
    {
        var link1 = new CardLink();
        var link2 = new CardLink();

        Assert.NotEqual(link1.Id, link2.Id);
    }

    [Fact]
    public void TodoItem_DefaultValues()
    {
        var todo = new TodoItem();

        Assert.NotEqual(Guid.Empty, todo.Id);
        Assert.Equal(string.Empty, todo.Text);
        Assert.False(todo.IsCompleted);
        Assert.True(todo.CreatedAt <= DateTime.UtcNow);
        Assert.Null(todo.CompletedAt);
        Assert.Equal(0, todo.Order);
    }

    [Fact]
    public void TodoItem_UniqueIds()
    {
        var todo1 = new TodoItem();
        var todo2 = new TodoItem();

        Assert.NotEqual(todo1.Id, todo2.Id);
    }

    [Fact]
    public void TodoItem_SetCompletedProperties()
    {
        var todo = new TodoItem
        {
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow
        };

        Assert.True(todo.IsCompleted);
        Assert.NotNull(todo.CompletedAt);
    }

    [Fact]
    public void Board_CanAddLanesAndCards()
    {
        var board = new Board { Name = "Test Board" };
        var lane = new Lane { Name = "To Do", Order = 0 };
        var card = new Card { Title = "Task 1", Description = "Description 1" };
        var todo = new TodoItem { Text = "Sub task" };
        var link = new CardLink { Title = "Link", Url = "https://example.com" };

        card.Todos.Add(todo);
        card.Links.Add(link);
        lane.Cards.Add(card);
        board.Lanes.Add(lane);

        Assert.Single(board.Lanes);
        Assert.Single(board.Lanes[0].Cards);
        Assert.Single(board.Lanes[0].Cards[0].Todos);
        Assert.Single(board.Lanes[0].Cards[0].Links);
        Assert.Equal("Task 1", board.Lanes[0].Cards[0].Title);
        Assert.Equal("Sub task", board.Lanes[0].Cards[0].Todos[0].Text);
        Assert.Equal("https://example.com", board.Lanes[0].Cards[0].Links[0].Url);
    }

    [Fact]
    public void BoardCollection_TracksLastOpenedBoard()
    {
        var board = new Board();
        var collection = new BoardCollection
        {
            LastOpenedBoardId = board.Id
        };
        collection.Boards.Add(board);

        Assert.Equal(board.Id, collection.LastOpenedBoardId);
        Assert.Single(collection.Boards);
    }
}
