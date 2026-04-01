# ProjectManagementApp

A Kanban-style project management application built with Blazor Server (.NET 10). All data is persisted locally as a JSON file — no database or external services required.

## Features

- **Multiple boards** with customizable labels for lanes, cards, and todos
- **Lanes** (swimlanes) for organizing cards into columns
- **Cards** with title, description, notes, links, todos, and completion tracking
- **Drag-and-drop** card movement between lanes
- **Board-level and card-level todos** with a dedicated sidebar panel
- **Show/hide completed items** toggle
- **Collapse/expand all** cards for quick overview
- **Last-opened board memory** — reopens where you left off
- **Backup** with timestamped file export
- **Dark and light mode** support
- **Responsive design** with mobile-friendly modals

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started

```bash
# Clone the repo
git clone <repo-url>
cd ProjectManagementApp

# Run the app
dotnet run
```

The app launches at **http://localhost:5148** by default (configured in `Properties/launchSettings.json`).

## Data Storage

### File Location

All board data is stored in a single JSON file:

```
{MyDocuments}\BoardCollection.json
```

On Windows this is typically:

```
C:\Users\{YourUsername}\Documents\BoardCollection.json
```

The path is resolved at runtime using `Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)`.

An info banner at the top of the board displays the exact file path for your system.

### JSON Structure

The file uses **camelCase** property naming and is written with indented formatting for readability.

```jsonc
{
  "boards": [
    {
      "id": "guid",
      "name": "My Board",
      "laneLabel": "Lane",       // Customizable label for lanes
      "cardLabel": "Card",       // Customizable label for cards
      "todoLabel": "Todo",       // Customizable label for todos
      "lanes": [
        {
          "id": "guid",
          "name": "To Do",
          "order": 0,
          "createdAt": "2026-01-01T00:00:00Z",
          "cards": [
            {
              "id": "guid",
              "title": "Card Title",
              "description": "Card description",
              "notes": "Free-form notes",
              "isCompleted": false,
              "createdAt": "2026-01-01T00:00:00Z",
              "completedAt": null,
              "order": 0,
              "todos": [
                {
                  "id": "guid",
                  "text": "Sub-task",
                  "isCompleted": false,
                  "createdAt": "2026-01-01T00:00:00Z",
                  "completedAt": null,
                  "order": 0
                }
              ],
              "links": [
                {
                  "id": "guid",
                  "title": "Link Title",
                  "url": "https://example.com",
                  "createdAt": "2026-01-01T00:00:00Z"
                }
              ]
            }
          ]
        }
      ],
      "todos": [
        // Board-level todos (same shape as card todos)
      ],
      "createdAt": "2026-01-01T00:00:00Z",
      "lastModified": "2026-01-01T00:00:00Z"
    }
  ],
  "lastOpenedBoardId": "guid"
}
```

### Backup

The app includes a backup button that copies `BoardCollection.json` to a timestamped file in the same Documents folder.

## Project Structure

```
ProjectManagementApp/
├── Components/
│   ├── Pages/
│   │   ├── Home.razor          # Main Kanban board UI and logic
│   │   ├── Error.razor         # Error page
│   │   └── NotFound.razor      # 404 page
│   ├── Layout/
│   │   ├── MainLayout.razor    # App shell layout
│   │   ├── NavMenu.razor       # Sidebar navigation
│   │   └── ReconnectModal.razor# Blazor Server reconnect UI
│   ├── App.razor               # Root component
│   ├── Routes.razor            # Router configuration
│   └── _Imports.razor          # Global using directives
├── Models/
│   └── KanbanModels.cs         # Data models (BoardCollection, Board, Lane, Card, CardLink, TodoItem)
├── Services/
│   └── KanbanService.cs        # BoardService — JSON file read/write and all CRUD operations
├── Properties/
│   └── launchSettings.json     # Dev server URL and environment config
├── wwwroot/                    # Static assets (CSS, Bootstrap)
├── ProjectManagementApp.Tests/
│   ├── UnitTest1.cs            # bUnit component tests for Home page
│   └── ProjectManagementApp.Tests.csproj
├── Program.cs                  # App startup and service registration
├── ProjectManagementApp.csproj # .NET 10, Blazor Server Web SDK
└── ProjectManagementApp.sln
```

## Data Models

| Model | Purpose |
|-------|---------|
| `BoardCollection` | Root object — list of boards and the last-opened board ID |
| `Board` | A named board with lanes, board-level todos, and custom labels |
| `Lane` | A column within a board containing ordered cards |
| `Card` | A work item with title, description, notes, links, todos, and completion state |
| `CardLink` | A titled URL attached to a card |
| `TodoItem` | A checklist item (used at both board and card level) |

All entities use `Guid` IDs generated at creation time.

## Running Tests

```bash
cd ProjectManagementApp.Tests
dotnet test
```

Tests use [bUnit](https://bunit.dev/) (v2.6.2) and [xUnit](https://xunit.net/) (v2.9.3) to verify that the Home page renders key UI elements (board name, board selector, info banner, completed toggle, new board button).

## Tech Stack

- **Framework**: .NET 10 / ASP.NET Core
- **UI**: Blazor Server with Interactive Server render mode
- **Styling**: Scoped CSS + Bootstrap 5
- **Persistence**: Local JSON file (no database)
- **Testing**: xUnit + bUnit
