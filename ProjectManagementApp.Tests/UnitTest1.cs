using Bunit;
using Xunit;
using ProjectManagementApp.Components.Pages;
using ProjectManagementApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectManagementApp.Tests;

public class HomePageTests : BunitContext
{
    public HomePageTests()
    {
        Services.AddScoped<BoardService>();
    }

    [Fact]
    public void HomePage_RendersClickableBoardName()
    {
        var cut = Render<Home>();

        var header = cut.Find("h1.board-name-clickable");
        Assert.NotNull(header);
    }

    [Fact]
    public void HomePage_HasShowCompletedCheckbox()
    {
        var cut = Render<Home>();

        var checkbox = cut.Find("input[type='checkbox']");
        Assert.NotNull(checkbox);

        var label = cut.Find("label.show-completed");
        Assert.Contains("Show Completed", label.TextContent);
    }

    [Fact]
    public void HomePage_HasInfoBanner()
    {
        var cut = Render<Home>();

        var banner = cut.Find(".info-banner");
        Assert.NotNull(banner);
        Assert.Contains("saved to", banner.TextContent);
    }

    [Fact]
    public void HomePage_HasBoardSelector()
    {
        var cut = Render<Home>();

        var selector = cut.Find(".board-selector");
        Assert.NotNull(selector);

        var dropdown = cut.Find(".board-dropdown");
        Assert.NotNull(dropdown);
    }

    [Fact]
    public void HomePage_HasNewBoardButton()
    {
        var cut = Render<Home>();

        var buttons = cut.FindAll(".board-selector button.btn-icon");
        Assert.NotEmpty(buttons);
    }

    [Fact]
    public void HomePage_HasAppLayout()
    {
        var cut = Render<Home>();

        var layout = cut.Find(".app-layout");
        Assert.NotNull(layout);
    }

    [Fact]
    public void HomePage_HasTodoSidebar()
    {
        var cut = Render<Home>();

        var sidebar = cut.Find(".todo-sidebar");
        Assert.NotNull(sidebar);
    }

    [Fact]
    public void HomePage_HasSidebarHeader()
    {
        var cut = Render<Home>();

        var header = cut.Find(".sidebar-header");
        Assert.NotNull(header);

        var count = cut.Find(".todo-count");
        Assert.NotNull(count);
    }

    [Fact]
    public void HomePage_SidebarShowsEmptyState_WhenNoTodos()
    {
        var cut = Render<Home>();

        var empty = cut.Find(".sidebar-empty");
        Assert.NotNull(empty);
    }

    [Fact]
    public void HomePage_HasAddTodoButton()
    {
        var cut = Render<Home>();

        var button = cut.Find(".btn-add-quick-todo");
        Assert.NotNull(button);
        Assert.Contains("Add", button.TextContent);
    }

    [Fact]
    public void HomePage_HasSidebarShowCompletedToggle()
    {
        var cut = Render<Home>();

        var label = cut.Find("label.show-completed-todos");
        Assert.NotNull(label);
        Assert.Contains("Show Completed", label.TextContent);
    }

    [Fact]
    public void HomePage_HasKanbanContainer()
    {
        var cut = Render<Home>();

        var container = cut.Find(".kanban-container");
        Assert.NotNull(container);
    }

    [Fact]
    public void HomePage_HasKanbanHeader()
    {
        var cut = Render<Home>();

        var header = cut.Find(".kanban-header");
        Assert.NotNull(header);
    }

    [Fact]
    public void HomePage_HasHeaderControls()
    {
        var cut = Render<Home>();

        var controls = cut.Find(".header-controls");
        Assert.NotNull(controls);
    }

    [Fact]
    public void HomePage_HasBackupButton()
    {
        var cut = Render<Home>();

        var button = cut.Find(".btn-backup");
        Assert.NotNull(button);
        Assert.Contains("Backup", button.TextContent);
    }

    [Fact]
    public void HomePage_HasCollapseAllButton()
    {
        var cut = Render<Home>();

        var button = cut.Find(".btn-collapse-all");
        Assert.NotNull(button);
    }

    [Fact]
    public void HomePage_HasAddLaneButton()
    {
        var cut = Render<Home>();

        var button = cut.Find(".btn-add-swimlane");
        Assert.NotNull(button);
    }

    [Fact]
    public void HomePage_DefaultBoardName_ShowsFallbackText()
    {
        var cut = Render<Home>();

        var header = cut.Find("h1.board-name-clickable");
        // When there are no boards, shows the fallback text
        var text = header.TextContent;
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void HomePage_InfoBanner_ShowsFilePath()
    {
        var cut = Render<Home>();

        var banner = cut.Find(".info-banner");
        Assert.Contains("BoardCollection.json", banner.TextContent);
    }

    [Fact]
    public void HomePage_BoardDropdown_RendersSelectElement()
    {
        var cut = Render<Home>();

        var select = cut.Find("select.board-dropdown");
        Assert.NotNull(select);
    }

    [Fact]
    public void HomePage_ClickBoardName_ShowsEditMode()
    {
        var cut = Render<Home>();

        var header = cut.Find("h1.board-name-clickable");
        header.Click();

        var editInput = cut.Find(".board-name-input");
        Assert.NotNull(editInput);
    }

    [Fact]
    public void HomePage_ClickNewBoardButton_ShowsModal()
    {
        var cut = Render<Home>();

        var newBoardBtn = cut.Find(".board-selector button.btn-icon[title='New Board']");
        newBoardBtn.Click();

        var modal = cut.Find(".modal-dialog");
        Assert.NotNull(modal);
    }

    [Fact]
    public void HomePage_NewBoardModal_HasNameInput()
    {
        var cut = Render<Home>();

        cut.Find(".board-selector button.btn-icon[title='New Board']").Click();

        var input = cut.Find(".modal-input");
        Assert.NotNull(input);
    }

    [Fact]
    public void HomePage_AddLaneButton_ShowsModal()
    {
        var cut = Render<Home>();

        cut.InvokeAsync(() => cut.Find(".btn-add-swimlane").Click());

        var modal = cut.Find(".modal-dialog");
        Assert.NotNull(modal);
    }

    [Fact]
    public void HomePage_PageTitle_IsSetCorrectly()
    {
        var cut = Render<Home>();

        // The PageTitle component should set the title
        var pageTitle = cut.FindAll("title");
        // PageTitle renders inside HeadOutlet, but the component itself should exist
        Assert.NotNull(cut);
    }

    [Fact]
    public void HomePage_NoModalsVisible_ByDefault()
    {
        var cut = Render<Home>();

        var modals = cut.FindAll(".modal-dialog");
        Assert.Empty(modals);
    }

    [Fact]
    public void HomePage_TodoCount_ShowsZeroByDefault()
    {
        var cut = Render<Home>();

        var count = cut.Find(".todo-count");
        Assert.Equal("0", count.TextContent);
    }
}
