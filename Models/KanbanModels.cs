namespace ProjectManagementApp.Models;

public class BoardCollection
{
    public List<Board> Boards { get; set; } = new();
    public Guid? LastOpenedBoardId { get; set; }
}

public class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Board";
    public string LaneLabel { get; set; } = "Lane";
    public string CardLabel { get; set; } = "Card";
    public string TodoLabel { get; set; } = "Todo";
    public bool IsCondensedView { get; set; } = false;
    public List<Lane> Lanes { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public class Lane
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Card> Cards { get; set; } = new();
}

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; }
    public List<TodoItem> Todos { get; set; } = new();
    public List<CardLink> Links { get; set; } = new();
}

public class CardLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsTodaysTodo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; }
}
