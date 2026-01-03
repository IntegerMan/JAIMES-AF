using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class ToolTestPage
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private ToolMetadataResponse[] _tools = [];
    private GameInfoResponse[] _games = [];
    private string? _selectedToolName;
    private ToolMetadataResponse? _selectedTool;
    private Guid? _selectedGameId;
    private Dictionary<string, string?> _parameterValues = new();
    private bool _isExecuting;
    private string? _errorMessage;
    private ToolExecutionResponse? _executionResult;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Tool Testing", href: null, disabled: true)
        };

        await Task.WhenAll(LoadToolsAsync(), LoadGamesAsync());
    }

    private async Task LoadToolsAsync()
    {
        try
        {
            ToolMetadataListResponse? response =
                await Http.GetFromJsonAsync<ToolMetadataListResponse>("/admin/tools/available");
            _tools = response?.Tools ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("ToolTestPage").LogError(ex, "Failed to load tools from API");
            _errorMessage = "Failed to load tools: " + ex.Message;
        }
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            ListGamesResponse? response = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
            _games = response?.Games ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("ToolTestPage").LogError(ex, "Failed to load games from API");
            _errorMessage = "Failed to load games: " + ex.Message;
        }
    }

    private Task OnToolSelectedAsync()
    {
        _selectedTool = _tools.FirstOrDefault(t => t.Name == _selectedToolName);
        _parameterValues.Clear();
        _executionResult = null;
        _errorMessage = null;

        // Initialize parameter values dictionary
        if (_selectedTool != null)
        {
            foreach (ToolParameterInfo param in _selectedTool.Parameters)
            {
                _parameterValues[param.Name] = param.DefaultValue;
            }
        }

        return Task.CompletedTask;
    }

    private bool CanExecute()
    {
        if (_selectedTool == null) return false;

        // Check if game context is required but not selected
        if (_selectedTool.RequiresGameContext && !_selectedGameId.HasValue) return false;

        // Check if all required parameters are provided
        foreach (ToolParameterInfo param in _selectedTool.Parameters)
        {
            if (param.IsRequired && string.IsNullOrWhiteSpace(_parameterValues.GetValueOrDefault(param.Name)))
            {
                return false;
            }
        }

        return true;
    }

    private async Task ExecuteToolAsync()
    {
        if (_selectedTool == null) return;

        _isExecuting = true;
        _errorMessage = null;
        _executionResult = null;

        try
        {
            ToolExecutionRequest request = new()
            {
                ToolName = _selectedTool.Name,
                GameId = _selectedGameId,
                Parameters = _parameterValues
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/admin/tools/execute", request);

            if (response.IsSuccessStatusCode)
            {
                _executionResult = await response.Content.ReadFromJsonAsync<ToolExecutionResponse>();
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _errorMessage = $"API error: {response.StatusCode} - {errorContent}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("ToolTestPage").LogError(ex, "Failed to execute tool");
            _errorMessage = "Failed to execute tool: " + ex.Message;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    private static string FormatGameDisplay(GameInfoResponse game)
    {
        string display = game.Title ?? $"Game {game.GameId:N}";
        if (!string.IsNullOrWhiteSpace(game.ScenarioName))
        {
            display += $" ({game.ScenarioName})";
        }

        return display;
    }

    private static string FormatParameterLabel(ToolParameterInfo param)
    {
        return param.IsRequired ? $"{param.Name} *" : param.Name;
    }

    private static string FormatParameterHelperText(ToolParameterInfo param)
    {
        List<string> parts = new();

        parts.Add($"Type: {param.TypeName}");

        if (!string.IsNullOrWhiteSpace(param.Description))
        {
            parts.Add(param.Description);
        }

        if (!string.IsNullOrWhiteSpace(param.DefaultValue))
        {
            parts.Add($"Default: {param.DefaultValue}");
        }

        return string.Join(" | ", parts);
    }

    private static string GetParameterPlaceholder(ToolParameterInfo param)
    {
        return param.TypeName switch
        {
            "string" => "Enter text...",
            "string?" => "Enter text (optional)...",
            "int" => "Enter a number...",
            "int?" => "Enter a number (optional)...",
            "bool" => "true or false",
            "bool?" => "true or false (optional)",
            "guid" => "Enter a GUID...",
            "guid?" => "Enter a GUID (optional)...",
            _ => $"Enter {param.TypeName}..."
        };
    }
}
