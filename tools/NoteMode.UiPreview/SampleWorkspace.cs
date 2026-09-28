using NoteMode.Services;
using NoteMode.ViewModels;

namespace NoteMode.UiPreview;

/// <summary>
/// A believable session for screenshots: a small project folder on disk, several open tabs (one
/// with unsaved edits) and a few notes in folders. Everything lives under NOTEMODE_HOME (a temp
/// folder set by Program), never in the real ~/.notemode.
/// </summary>
internal sealed class SampleWorkspace
{
    public required MainWindowViewModel ViewModel { get; init; }
    public required SyntaxService Syntax { get; init; }
    public required string ProjectDir { get; init; }
    public required TabViewModel CodeTab { get; init; }
    public required TabViewModel ReadmeTab { get; init; }
    public required TabViewModel MeetingNote { get; init; }

    public static SampleWorkspace Create(string root)
    {
        // Each sample starts from an empty profile (the previous one's session would be restored).
        if (Directory.Exists(AppPaths.DataDir))
            Directory.Delete(AppPaths.DataDir, recursive: true);

        var project = Path.Combine(root, "acme-orders");
        Directory.CreateDirectory(Path.Combine(project, "src", "Services"));
        Directory.CreateDirectory(Path.Combine(project, "src", "Models"));
        Directory.CreateDirectory(Path.Combine(project, "scripts"));
        Directory.CreateDirectory(Path.Combine(project, "docs"));

        var code = Write(project, "src/Services/OrderService.cs", OrderService);
        Write(project, "src/Models/Order.cs", OrderModel);
        var readme = Write(project, "README.md", Readme);
        var settings = Write(project, "appsettings.json", AppSettings);
        Write(project, "scripts/deploy.ps1", Deploy);
        var log = Write(project, "docs/release-notes.txt", ReleaseNotes);

        var syntax = new SyntaxService();
        var vm = new MainWindowViewModel(new StateService(), new CacheService(), syntax, new FileChangeService(), new NoteService())
        {
            WindowWidth = 1440,
            WindowHeight = 820
        };

        // Notes first, so they sit to the left of the files like a real session.
        var meeting = CreateNote(vm, "Standup notes", "MarkDown", MeetingNotes);
        var ideas = CreateNote(vm, "Ideas", "Plain Text", Ideas);
        var snippets = CreateNote(vm, "SQL snippets", "TSQL", Snippets);
        var work = vm.NoteService.CreateFolder("Work", null);
        var personal = vm.NoteService.CreateFolder("Personal", null);
        vm.NoteService.MoveNote(meeting.Id, work.Id);
        vm.NoteService.MoveNote(snippets.Id, work.Id);
        vm.NoteService.MoveNote(ideas.Id, personal.Id);
        vm.CloseTab(ideas);
        vm.CloseTab(snippets);

        var codeTab = vm.OpenFile(code)!;
        var readmeTab = vm.OpenFile(readme)!;
        vm.OpenFile(settings);
        vm.OpenFile(log);

        // An unsaved edit, so the tab strip shows the dot a session keeps across restarts.
        codeTab.Content = codeTab.Content.Replace("// TODO: retry transient failures", "// Retry transient failures up to three times");

        vm.SelectedTab = codeTab;
        return new SampleWorkspace { ViewModel = vm, Syntax = syntax, ProjectDir = project, CodeTab = codeTab, ReadmeTab = readmeTab, MeetingNote = meeting };
    }

    private static TabViewModel CreateNote(MainWindowViewModel vm, string title, string syntax, string content)
    {
        vm.NewTab();
        var tab = vm.SelectedTab!;
        tab.Content = content;
        tab.SyntaxName = syntax;
        vm.RenameTab(tab, title);
        vm.SaveAsNote(tab);
        return tab;
    }

    private static string Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
        return path;
    }

    private const string OrderService = """
        using System.Collections.Concurrent;
        using Acme.Orders.Models;
        using Microsoft.Extensions.Logging;

        namespace Acme.Orders.Services;

        /// <summary>Places orders and keeps a short-lived cache of recent ones.</summary>
        public sealed class OrderService(IOrderRepository repository, ILogger<OrderService> logger)
        {
            private readonly ConcurrentDictionary<Guid, Order> _recent = new();

            public async Task<Order> PlaceAsync(Cart cart, CancellationToken ct = default)
            {
                if (cart.Items.Count == 0)
                    throw new InvalidOperationException("Cannot place an empty order.");

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = cart.CustomerId,
                    Lines = cart.Items.Select(i => new OrderLine(i.Sku, i.Quantity, i.UnitPrice)).ToList(),
                    PlacedAt = DateTimeOffset.UtcNow
                };

                // TODO: retry transient failures
                await repository.SaveAsync(order, ct);
                _recent[order.Id] = order;

                logger.LogInformation("Order {OrderId} placed: {Total:C}", order.Id, order.Total);
                return order;
            }

            public Order? FindRecent(Guid id) => _recent.TryGetValue(id, out var order) ? order : null;
        }
        """;

    private const string OrderModel = """
        namespace Acme.Orders.Models;

        public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice)
        {
            public decimal Total => Quantity * UnitPrice;
        }

        public sealed class Order
        {
            public Guid Id { get; init; }
            public Guid CustomerId { get; init; }
            public List<OrderLine> Lines { get; init; } = [];
            public DateTimeOffset PlacedAt { get; init; }
            public decimal Total => Lines.Sum(l => l.Total);
        }
        """;

    private const string Readme = """
        # Acme Orders

        Order placement service for the **Acme** storefront.

        ## Getting started

        1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download)
        2. Copy `appsettings.json` and set your connection string
        3. Run `dotnet run --project src/Acme.Orders`

        ## Endpoints

        | Method | Path | Description |
        |--------|------|-------------|
        | `POST` | `/orders` | Place an order |
        | `GET` | `/orders/{id}` | Look up an order |

        > Orders are cached for five minutes after they are placed.

        - [x] Place orders
        - [x] Structured logging
        - [ ] Retry transient database failures
        """;

    private const string AppSettings = """
        {
          "ConnectionStrings": {
            "Orders": "Server=localhost;Database=orders;Trusted_Connection=True"
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "Cache": { "RecentOrderMinutes": 5 },
          "AllowedHosts": "*"
        }
        """;

    private const string Deploy = """
        param([string]$Environment = "staging")

        $ErrorActionPreference = "Stop"
        Write-Host "Deploying Acme Orders to $Environment..."
        dotnet publish src/Acme.Orders -c Release -o publish
        az webapp deploy --name "acme-orders-$Environment" --src-path publish.zip
        """;

    private const string ReleaseNotes = """
        Acme Orders - release notes

        1.4.0
          - Orders are cached for five minutes after they are placed
          - Structured logging for every order
          - Fixed: empty carts could be submitted

        1.3.2
          - Fixed rounding of multi-currency totals
        """;

    private const string MeetingNotes = """
        # Standup - Tuesday

        ## Done
        - Shipped order caching (1.4.0)
        - Reviewed the payments PR

        ## Today
        - [ ] Retry transient DB failures
        - [ ] Load test `/orders` at 500 rps

        ## Blockers
        None.
        """;

    private const string Ideas = """
        Weekend project ideas
        - Plant watering reminder
        - Recipe scaler
        - Bike route logger
        """;

    private const string Snippets = """
        -- Orders placed in the last day
        SELECT o.Id, o.CustomerId, SUM(l.Quantity * l.UnitPrice) AS Total
        FROM Orders o
        JOIN OrderLines l ON l.OrderId = o.Id
        WHERE o.PlacedAt > DATEADD(day, -1, SYSUTCDATETIME())
        GROUP BY o.Id, o.CustomerId
        ORDER BY Total DESC;
        """;
}
