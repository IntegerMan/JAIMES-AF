# JAIMES AF UI Style Guide

This document defines the UI conventions, patterns, and reusable components used throughout the JAIMES AF Blazor application. It is intended to help AI agents and developers maintain consistency when creating or modifying UI code.

**Primary Reference Pages**: `Home.razor` and `Admin.razor` are the gold standard for dashboard styling. `Games.razor` is the gold standard for list pages. The sidebar (`NavMenu.razor`) demonstrates navigation patterns.

---

## Quick Reference

When building UI, follow these patterns:

- **Displaying an agent name** → Use `<AgentLink>` component
- **Displaying agent + version together** → Use `<AgentVersionDisplay>` component
- **Displaying a version number alone** → Use `<AgentVersionLink>` component (handles null versions with "Latest" chip)
- **List/tool/detail page headers** → Use `<CompactHeroSection>` component
- **Form page headers (create/edit)** → Use `<FormPageHeader>` component
- **Statistics/metrics display** → Use `<MetricCard>` component with appropriate `ColorVariant`
- **Comparison tables** → Use `<ComparisonMatrix>` component for version/metric comparisons
- **View mode toggles** → Use `<ViewModeToggle>` component for "Group by" or "View as" controls
- **Icon buttons** → Always wrap in `<MudTooltip Placement="Placement.Top">`
- **Tooltips** → Always use `Placement.Top` for better visibility
- **Agent/bot icon** → `Icons.Material.Filled.SmartToy` with `Color.Secondary`
- **Section labels** → Use `Typo.overline` with purple color and letter-spacing
- **Chat messages** → Use the `PlayerMessage`, `AssistantMessage`, or `ErrorMessage` components
- **List pages** → Follow the List Page Pattern (`Games.razor` is the gold standard)
- **Form pages** → Follow the Form Page Pattern (`NewAgent.razor`, `EditAgent.razor` are gold standards)
- **Test pages** → Follow patterns in `TestCaseDetails.razor`, `TestRunComparison.razor`

---

## Design Principles

These principles guide UI decisions across the application:

### Components Over CSS

**Prefer reusable Blazor components over CSS classes** for standardization and maintainability:
- ✅ Use `<MetricCard>` component instead of `.stat-card` CSS classes
- ✅ Use `<CompactHeroSection>` component instead of inline hero section markup
- ✅ Use `<ViewModeToggle>` component instead of raw `<MudToggleGroup>`

This approach ensures consistent styling, easier refactoring, and better type safety.

### Page Header Patterns

Use `CompactHeroSection` for **all non-form pages** (list, detail, tool, comparison pages):
- Provides consistent visual hierarchy across the application
- Supports dynamic content (item counts, action buttons)
- Includes theme-based color coordination

Use `FormPageHeader` only for **create/edit form pages**.

### Breadcrumb Placement

**Breadcrumbs go below the hero section**, not above:
```razor
<CompactHeroSection Title="..." ... />

<MudStack Row="true" AlignItems="AlignItems.Center" Class="mb-2">
    <MudBreadcrumbs Items="_breadcrumbs" Separator=">"></MudBreadcrumbs>
</MudStack>
```

This keeps the hero as the visual anchor and positions navigation aids closer to the content.

### Theme Assignments

Each entity type has an assigned theme for visual consistency:

| Entity Type | Theme | Color |
|-------------|-------|-------|
| Games | Primary | Purple `#7B2CBF` |
| Agents, Scenarios | Secondary | Blue `#4361EE` |
| Players/Characters | Tertiary | Cyan `#4CC9F0` |
| Rulesets | Accent/Success | Green `#06D6A0` |
| Tests, Evaluations | Info | Light Blue `#2196F3` |
| ML Models, Classification | Error | Pink `#EF476F` |
| Sentiments | Primary | Purple `#7B2CBF` |

### Score Color Coding

When displaying scores (0.0 - 1.0), use consistent color thresholds:
```csharp
private static MetricCard.MetricColorVariant GetScoreColorVariant(double? score) => score switch
{
    >= 0.8 => MetricCard.MetricColorVariant.Success,  // Green - excellent
    >= 0.6 => MetricCard.MetricColorVariant.Warning,  // Yellow - acceptable
    _ => MetricCard.MetricColorVariant.Error          // Red - needs attention
};
```

### Comparison Displays

For comparing metrics across versions or test cases:
- Use `ComparisonMatrix` component with sticky row labels
- Color-code cells based on score thresholds
- Include tooltips with detailed reasoning/remarks
- Show averages row for quick comparison
- Show timing information when relevant

---

## UI Framework

The application uses **MudBlazor** as the primary UI component library. All layouts and components should use MudBlazor's component system rather than raw HTML elements.

### Theme Colors

| Semantic Name | Hex Code | MudBlazor Color | Usage |
|---------------|----------|-----------------|-------|
| Primary | `#7B2CBF` | `Color.Primary` | Games, primary actions, main branding |
| Secondary | `#4361EE` | `Color.Secondary` | Agents, scenarios, secondary actions |
| Tertiary | `#4CC9F0` | `Color.Tertiary` | Players/characters |
| Accent/Success | `#06D6A0` | `Color.Success` | Rulesets, success states |
| Warning | `#FFD60A` | `Color.Warning` | Locations, warnings |
| Error | `#EF476F` | `Color.Error` | Errors, delete actions |
| Dark | `#2B2D42` | `Color.Dark` | Admin areas, dark backgrounds |

### CSS Custom Properties

Key CSS variables defined in `app.css`:

```css
--color-primary: #7B2CBF;
--color-secondary: #4361EE;
--color-tertiary: #4CC9F0;
--color-accent: #06D6A0;
--gradient-hero: linear-gradient(135deg, #7B2CBF 0%, #4361EE 50%, #4CC9F0 100%);
--color-glass: rgba(255, 255, 255, 0.85);
```

---

## Reusable Components

Use these components instead of building custom displays. They are located in `JAIMES AF.Web/Components/`.

### Display Components

| Component | Purpose |
|-----------|---------|
| `AgentLink.razor` | Displays agent name as a linked chip with bot icon (`SmartToy`). Use whenever showing an agent name. Has "View Agent Details" tooltip. |
| `AgentVersionLink.razor` | Displays version number as a linked chip. When `VersionId` is null, shows "Latest" chip (Primary, Filled) that links to agent page. Has appropriate tooltips. |
| `AgentVersionDisplay.razor` | Combined agent name + version number display. Use when showing both together. |
| `RulesetLink.razor` | Displays ruleset ID as a linked chip with book icon (`AutoStories`). Tooltip shows full name and description. Use whenever showing a ruleset ID. |
| `SentimentIcon.razor` | Sentiment indicator (thumbs up/down/neutral) with optional edit menu. Use in chat footers. |
| `MessageIndicators.razor` | Displays evaluation metrics and tool call badges. Use in assistant message footers. |

### Layout Components

| Component | Purpose |
|-----------|---------|
| `CompactHeroSection.razor` | Hero header for list, tool, and detail pages with icon badge, title, subtitle, and optional action button. Supports themes: Primary, Secondary, Tertiary, Accent, Success, Info, Error. Use for all non-form page headers. |
| `FormPageHeader.razor` | Hero header for create/edit form pages with icon badge, title, and subtitle. Matches CompactHeroSection styling without action button. |
| `CompactLinkCard.razor` | Navigation card with colored icon badge. Use for dashboard/home page links. |
| `MetricCard.razor` | Statistics display card with icon, value, label. Supports color variants, tooltips, click handlers, and navigation (Href). Use for dashboard stats and page summaries. |
| `MetricCardGrid.razor` | Responsive MudGrid wrapper for MetricCard components. Use when displaying multiple metrics in a row. |

### Data Visualization Components

| Component | Purpose |
|-----------|---------|
| `ComparisonMatrix.razor` | Generic comparison table with sticky row labels, color-coded cells, tooltips, and average rows. Use for comparing metrics across versions or test cases. Requires `TRow` and `TColumn` type parameters. |
| `ViewModeToggle.razor` | Generic toggle component for switching between view modes. Wraps MudToggleGroup with standardized styling. Use for "Group by" or "View as" toggles. |

### Chat Components

| Component | Purpose |
|-----------|---------|
| `PlayerMessage.razor` | Chat bubble for user/player messages. Use instead of raw `MudChat`. |
| `AssistantMessage.razor` | Chat bubble for AI/assistant messages with markdown support. Use instead of raw `MudChat`. |
| `ErrorMessage.razor` | Chat bubble for error messages with optional retry button. |

---

## Icon Conventions

### Entity Icons

Always use these specific icons for each entity type:

| Entity | Icon | Color |
|--------|------|-------|
| Agent/Bot | `Icons.Material.Filled.SmartToy` | `Color.Secondary` |
| Game | `Icons.Material.Filled.SportsEsports` | `Color.Primary` |
| Scenario | `Icons.Material.Filled.MenuBook` | `Color.Secondary` |
| Player/Character | `Icons.Material.Filled.Person` | `Color.Tertiary` |
| Ruleset | `Icons.Material.Filled.AutoStories` | `Color.Success` |
| Sourcebook | `Icons.Material.Filled.Book` | `Color.Info` |
| Location | `Icons.Material.Filled.Place` | `Color.Warning` |
| Version/Update | `Icons.Material.Filled.Update` | `Color.Info` |
| ML Model | `Icons.Material.Filled.Psychology` | `Color.Error` |

### Action Icons

| Action | Icon | Typical Color |
|--------|------|---------------|
| Add/Create | `Icons.Material.Filled.Add` | Entity-specific |
| View/Details | `Icons.Material.Filled.Visibility` | `Color.Primary` |
| Edit | `Icons.Material.Filled.Edit` | `Color.Default` |
| Delete | `Icons.Material.Filled.Delete` | `Color.Error` |
| Refresh | `Icons.Material.Filled.Refresh` | `Color.Default` |
| Improve/AI | `Icons.Material.Filled.AutoAwesome` | `Color.Secondary` |
| Test/Evaluate | `Icons.Material.Filled.Science` | `Color.Success` |
| Download/Export | `Icons.Material.Filled.Download` | `Color.Info` |
| Run/Play | `Icons.Material.Filled.PlayArrow` | `Color.Success` |
| Save/Confirm | `Icons.Material.Filled.Check` | Entity-specific |
| Cancel/Close | `Icons.Material.Filled.Close` | `Color.Error` |
| Tool Calls | `Icons.Material.Filled.Build` | `Color.Default` |

### Status Icons

| Status | Icon | Color |
|--------|------|-------|
| Success/Complete | `Icons.Material.Filled.CheckCircle` | `Color.Success` |
| Error | `Icons.Material.Filled.Error` | `Color.Error` |
| Warning | `Icons.Material.Filled.Warning` | `Color.Warning` |
| Info | `Icons.Material.Filled.Info` | `Color.Info` |
| Positive Feedback | `Icons.Material.Filled.ThumbUp` | `Color.Success` |
| Negative Feedback | `Icons.Material.Filled.ThumbDown` | `Color.Error` |
| Neutral Feedback | `Icons.Material.Filled.Balance` | `Color.Default` |

---

## Tooltip Standards

### Placement

**Always use `Placement.Top`** for tooltips. This ensures consistent visibility across the application.

```razor
<MudTooltip Text="View Details" Placement="Placement.Top">
    <MudIconButton Icon="@Icons.Material.Filled.Visibility" ... />
</MudTooltip>
```

### When to Add Tooltips

- ✅ All buttons (required)
- ✅ Chips that link to other pages
- ✅ Status indicators and icons
- ✅ Any element whose purpose isn't immediately obvious

### Complex Tooltips

For tooltips with structured content, use `TooltipContent`:

```razor
<MudTooltip Placement="Placement.Top">
    <TooltipContent>
        <div class="d-flex flex-column gap-1">
            <MudText Typo="Typo.subtitle2">Title</MudText>
            <MudText Typo="Typo.caption">Description text</MudText>
        </div>
    </TooltipContent>
    <ChildContent>
        <!-- Element to attach tooltip to -->
    </ChildContent>
</MudTooltip>
```

#### Interactive Chart Tooltips

For charts (like Pie charts or Sparklines), use dynamic tooltips that update based on mouse hover:
- **Per-Segment Detail**: Show the specific metric name and raw value.
- **Visual Aid**: Include a small colored indicator matching the chart segment.
- **Format**: Use `Metric Name: Value / Max` for clarity.

```razor
<MudTooltip Placement="Placement.Top">
    <TooltipContent>
        <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2" Class="pa-1">
            <div style="width: 10px; height: 12px; border-radius: 4px; background-color: @Color;"></div>
            <MudStack Spacing="0">
                <MudText Typo="Typo.caption" Style="font-weight: 700;">@MetricName</MudText>
                <MudText Typo="Typo.caption">@Score / 5.0</MudText>
            </MudStack>
        </MudStack>
    </TooltipContent>
    <ChildContent>
        <svg ... @onmouseover="@(() => _hoveredMetric = metric)">
            <!-- Chart content -->
        </svg>
    </ChildContent>
</MudTooltip>
```

---

## Link Styling

All navigational links should use **`Color.Primary`** (purple `#7B2CBF`) for consistency. This applies to:

- Entity names that navigate to detail pages (e.g., tool names, model names)
- Text links in data grids
- Inline navigation links

### Standard MudLink Pattern

```razor
<MudLink Href="@($"/items/{context.Id}")" 
         Color="Color.Primary" 
         Underline="Underline.Hover">
    @context.Name
</MudLink>
```

### When NOT to use Color.Primary for links

- **Semantic feedback links**: Use `Color.Success` or `Color.Error` when the link represents positive/negative feedback navigation
- **Component links** (AgentLink, RulesetLink, etc.): These use chips with entity-specific colors
- **CompactLinkCard**: Uses the card's theme color for the icon badge, link itself has no underline

### Typography for Grid Links

For links inside data grids, use:

```razor
<MudLink Href="@url" 
         Color="Color.Primary" 
         Typo="Typo.body2" 
         Underline="Underline.Hover">
    @text
</MudLink>
```

---

## Anti-Patterns

Avoid these common mistakes:

### Agent/Entity Display

- ❌ **Don't** display agent names as plain text or raw `<a>` links
- ✅ **Do** use `<AgentLink AgentId="@id" AgentName="@name" />`

- ❌ **Don't** display version numbers as plain text when they should link
- ✅ **Do** use `<AgentVersionLink>` or `<AgentVersionDisplay>`

### Tooltips

- ❌ **Don't** use `Placement.Bottom` or other placements
- ✅ **Do** use `Placement.Top` for all tooltips

- ❌ **Don't** leave icon buttons without tooltips
- ✅ **Do** wrap every icon button in a `MudTooltip`

### Icons

- ❌ **Don't** use arbitrary icons for agents (like `Person` or `Robot`)
- ✅ **Do** use `Icons.Material.Filled.SmartToy` for agents

- ❌ **Don't** use inconsistent colors for entity icons
- ✅ **Do** follow the entity icon color table above

### Layout

- ❌ **Don't** use raw `<div>` for page containers
- ✅ **Do** use `MudContainer` and `MudPaper` for page structure

- ❌ **Don't** use raw `<table>` elements
- ✅ **Do** use `MudTable` with `Dense="true"` and `Hover="true"`

### Forms

- ❌ **Don't** use raw `<input>` elements
- ✅ **Do** use `MudTextField` with `Variant="Variant.Outlined"`

---

## Chat Message Patterns

### User Messages (Player)

Use `PlayerMessage.razor` component or:

```razor
<MudChat ChatPosition="ChatBubblePosition.End" 
         Variant="Variant.Filled" 
         Color="Color.Default">
    <MudChatHeader Name="Player"/>
    <MudChatBubble>@Text</MudChatBubble>
</MudChat>
```

### Assistant Messages (Game Master)

Use `AssistantMessage.razor` component or:

```razor
<MudChat ChatPosition="ChatBubblePosition.Start" 
         Variant="Variant.Filled" 
         Color="Color.Default">
    <MudChatHeader Name="Game Master"/>
    <MudChatBubble>
        @((MarkupString) ChatHelpers.RenderMarkdown(Text))
    </MudChatBubble>
    <MudChatFooter>
        <!-- Agent version link, MessageIndicators, etc. -->
    </MudChatFooter>
</MudChat>
```

### Error Messages

Use `ErrorMessage.razor` component or:

```razor
<MudChat ChatPosition="ChatBubblePosition.Start" 
         Variant="Variant.Filled" 
         Color="Color.Error">
    <MudChatHeader Name="System"/>
    <MudChatBubble>
        <MudIcon Icon="@Icons.Material.Filled.Error" Size="Size.Small"/>
        @ErrorMessage
    </MudChatBubble>
</MudChat>
```

### Chat Footer Patterns

Assistant message footers should include:
- Agent version indicator (bot icon linking to version details)
- `MessageIndicators` component for metrics and tool calls
- Sentiment controls if feedback is enabled

---

## Page Layout Patterns

### Standard Page Structure

```razor
<PageTitle>Page Name</PageTitle>

<MudPaper Class="pa-4">
    <MudStack Row="true" AlignItems="AlignItems.Center" Class="mb-2">
        <MudBreadcrumbs Items="_breadcrumbs" Separator=">"></MudBreadcrumbs>
    </MudStack>
    
    <MudText Typo="Typo.h4">Page Title</MudText>
    <MudText Class="mb-4">Description text.</MudText>

    <!-- Page content -->
</MudPaper>
```

### Form Page Structure

Form pages (create/edit) should use the `FormPageHeader` component for visual consistency with list pages:

```razor
<MudContainer MaxWidth="MaxWidth.Medium" Class="mt-4">
    <FormPageHeader Title="Create New Agent"
                    Icon="@Icons.Material.Filled.SmartToy"
                    Theme="FormPageHeader.HeroTheme.Secondary"
                    Subtitle="Define a new AI agent" />

    <MudPaper Class="pa-6" Elevation="2">
        <MudStack Row="true" AlignItems="AlignItems.Center" Class="mb-4">
            <MudBreadcrumbs Items="_breadcrumbs" Separator=">"></MudBreadcrumbs>
        </MudStack>

        <MudForm>
            <MudTextField @bind-Value="_name" Label="Name" 
                          Variant="Variant.Outlined" Class="mb-4" />
            
            <div class="d-flex gap-2 mt-4">
                <MudButton Variant="Variant.Filled" 
                           Color="Color.Secondary"
                           StartIcon="@Icons.Material.Filled.Add">
                    Create Agent
                </MudButton>
                <MudButton Variant="Variant.Text" 
                           Color="Color.Default"
                           Href="/agents">
                    Cancel
                </MudButton>
            </div>
        </MudForm>
    </MudPaper>
</MudContainer>
```

**Form Page Guidelines:**
- Use `MaxWidth.Medium` for simple forms, `MaxWidth.Large` for complex multi-column forms
- Use `pa-6` and `Elevation="2"` on the main `MudPaper`
- **Create buttons**: Use `StartIcon="@Icons.Material.Filled.Add"` with entity-specific color
- **Save buttons**: Use `StartIcon="@Icons.Material.Filled.Check"` with entity-specific color
- **Cancel buttons**: Use `Variant.Text`, `Color.Default`, and `Href` to navigate back (not `OnClick`)
- Use entity-specific button colors (see Entity Icon table for color mapping)

**Edit Page Titles:**
For edit pages, use dynamic titles that show the entity name after loading:
```razor
<FormPageHeader Title="@(_isLoading ? "Edit Agent" : $"Edit: {_name}")" ... />
```

### Hero Section (Home/Landing Pages)

```razor
<div class="hero-premium pa-4 mb-5">
    <MudGrid>
        <MudItem xs="12">
            <h1 class="hero-title">Title</h1>
            <p class="hero-subtitle">Subtitle text</p>
        </MudItem>
    </MudGrid>
</div>
```

### List Page Pattern

**Reference page:** `Games.razor` — the gold standard for list pages.

List pages should include these elements:

#### Compact Hero Section

Use the `CompactHeroSection` component for a lightweight header with entity icon, item count, and primary action:

```razor
<CompactHeroSection Title="Games"
                    Icon="@Icons.Material.Filled.SportsEsports"
                    Theme="CompactHeroSection.HeroTheme.Primary"
                    ItemCount="@_games?.Count"
                    ItemName="game"
                    Subtitle="available"
                    SubtitleNoItems="Create your first game"
                    ActionText="New Game"
                    ActionHref="/games/new"
                    ActionIcon="@Icons.Material.Filled.Add"/>
```

Available themes:
- **Primary**: Purple - Games
- **Secondary**: Blue - Agents, Scenarios
- **Tertiary**: Cyan - Characters/Players
- **Accent/Success**: Green - Rulesets
- **Info**: Light Blue - Tests, Evaluations
- **Error**: Pink - ML Models, Classification

#### Table Action Buttons

Use icon buttons with tooltips instead of text buttons. Standard pattern for row actions:

```razor
<MudTd Style="text-align: right;">
    <MudTooltip Text="View Details" Placement="Placement.Top">
        <MudIconButton Icon="@Icons.Material.Filled.PlayArrow" Color="Color.Primary" Size="Size.Small"
                       Href="@($"/items/{context.Id}")" />
    </MudTooltip>
    <MudTooltip Text="Delete Item" Placement="Placement.Top">
        <MudIconButton Icon="@Icons.Material.Filled.Delete" Color="Color.Error" Size="Size.Small"
                       OnClick="@(() => DeleteItemAsync(context.Id))"/>
    </MudTooltip>
</MudTd>
```

#### Agent Version Display

Use `AgentVersionLink` to display agent versions - it handles null versions by showing a "Latest" chip:

```razor
<AgentLink AgentId="@context.AgentId" AgentName="@context.AgentName" />
<AgentVersionLink AgentId="@context.AgentId" 
                  VersionId="@context.InstructionVersionId" 
                  VersionNumber="@context.VersionNumber" />
```

When `VersionId` is null, displays "Latest" with `Color.Primary` and links to agent page.

#### Empty State

Show an engaging empty state when no items exist:

```razor
@if (_items?.Length == 0)
{
    <div class="glass-card pa-6" style="text-align: center;">
        <MudIcon Icon="@Icons.Material.Filled.SportsEsports" Size="Size.Large" 
                 Color="Color.Primary" Class="mb-3" Style="font-size: 4rem; opacity: 0.6;" />
        <MudText Typo="Typo.h6" Class="mb-2">No items yet</MudText>
        <MudText Class="mb-4 text-muted">Description encouraging action.</MudText>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" 
                   StartIcon="@Icons.Material.Filled.Add" Href="/items/new">
            Create Your First Item
        </MudButton>
    </div>
}
```

### Section Headers

Use these for grouping content within a page. Prefer left alignment and ALL CAPS HEADERS.

```razor
<MudText Typo="Typo.overline" Class="mb-3" 
         Style="letter-spacing: 1.5px; color: #7B2CBF; font-weight: 600;">
    SECTION NAME
</MudText>
```

---

## Card Patterns

### Glass Card

Used for prominent content areas:

```razor
<div class="glass-card pa-4">
    <!-- Content -->
</div>
```

### MetricCard Component (Recommended)

Use the `MetricCard` component for displaying statistics and metrics. It provides consistent styling with color variants, tooltips, and click/navigation support:

```razor
@* Basic metric display *@
<MetricCard Icon="@Icons.Material.Filled.Assessment" 
            Value="42" 
            Label="Test Cases" 
            ColorVariant="MetricCard.MetricColorVariant.Info"/>

@* Clickable metric with navigation *@
<MetricCard Icon="@Icons.Material.Filled.SportsEsports" 
            Value="@_gamesCount.ToString()" 
            Label="Active Games" 
            ColorVariant="MetricCard.MetricColorVariant.Primary"
            Href="/games"
            Tooltip="Click to view all games"/>

@* Multiple metrics in a responsive grid *@
<MudGrid Spacing="3">
    <MudItem xs="12" sm="4">
        <MetricCard Icon="@Icons.Material.Filled.PlayArrow" Value="15" Label="Total Runs" />
    </MudItem>
    <MudItem xs="12" sm="4">
        <MetricCard Icon="@Icons.Material.Filled.Score" Value="0.87" Label="Avg Score" 
                    ColorVariant="MetricCard.MetricColorVariant.Success"/>
    </MudItem>
    <MudItem xs="12" sm="4">
        <MetricCard Icon="@Icons.Material.Filled.Timer" Value="1250 ms" Label="Avg Duration" />
    </MudItem>
</MudGrid>
```

Color variants: `Primary`, `Secondary`, `Tertiary`, `Accent`, `Success`, `Warning`, `Error`, `Info`

### Stat Card (Legacy)

> **Note**: Prefer `MetricCard` component for new development. `stat-card` CSS classes are still used in some pages pending migration.

```razor
<div class="stat-card stat-card-primary">
    <MudText Typo="Typo.caption">Label</MudText>
    <MudText Typo="Typo.h6">Value</MudText>
</div>
```

Available variants: `stat-card-primary`, `stat-card-secondary`, `stat-card-tertiary`, `stat-card-accent`, `stat-card-warning`, `stat-card-error`

### ComparisonMatrix Component

Use for comparing metrics across multiple versions, test cases, or time periods. Features sticky row labels, color-coded cells, and tooltips:

```razor
<ComparisonMatrix TRow="int" TColumn="VersionResult"
                  Rows="_testCaseIds"
                  Columns="_versionResults"
                  RowLabel="@(testCaseId => GetTestCaseName(testCaseId))"
                  ColumnHeader="@(vr => $"{vr.AgentName} v{vr.VersionNumber}")"
                  CellValue="@((tcId, vr) => GetMetricValue(tcId, vr))"
                  CellColor="@((tcId, vr) => GetCellColor(tcId, vr))"
                  CellTooltip="@((tcId, vr) => GetCellTooltip(tcId, vr))"
                  ShowAverages="true"
                  AverageValue="@(vr => vr.AvgScore?.ToString("F2") ?? "-")"
                  ShowTimes="true"
                  TimeValue="@(vr => $"{vr.AvgDuration:F0} ms")"/>
```

### ViewModeToggle Component

Use for "Group by" or "View as" toggle controls:

```razor
@* Define options *@
private static readonly ViewModeToggle<string>.ViewModeOption<string>[] _viewModeOptions =
[
    new("agent", "Group by Agent", Icons.Material.Filled.Person),
    new("testcase", "Group by Test Case", Icons.Material.Filled.Science)
];

@* Use in markup *@
<ViewModeToggle TValue="string" @bind-Value="_groupBy" Options="_viewModeOptions"/>
```

#### Message Logs & Data Grids

When displaying message logs (e.g., `AgentMessagesList.razor`), adhere to the following standards:

1.  **Sender Identification**: 
    - Always include a **Sender** column before the message text.
    - Use distinct visual styles to distinguish participant types:
        - **Players**: Use a `MudChip` (Color.Info) with `Icons.Material.Filled.Person`.
        - **AI Agents / GM**: Use an `AgentVersionLink` or similar navigation control to link to the specific agent version.
    - Display the participant's name clearly.
2.  **Interactive Elements**: See [Interactive Chart Tooltips](#interactive-chart-tooltips) for standardizing wedge hovers.

#### Metric Value Links

When a metric count or value in a dashboard stat card acts as a link (or triggers an on-click action like switching tabs), use the primary theme color to clearly indicate it is interactive:
- **Color**: `var(--mud-palette-primary)`
- **Font Weight**: `600` (Semi-bold)

```razor
<MudText Typo="Typo.h6" Style="font-weight: 600; color: var(--mud-palette-primary);">
    @_count
</MudText>
```

### Compact Link Card

Use the `CompactLinkCard` component for navigation cards on dashboards.

---

## Table Patterns

```razor
<MudTable Items="_items" Dense="true" Hover="true">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh Style="text-align: right;">Actions</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd>
            <MudLink Href="@($"/items/{context.Id}")">@context.Name</MudLink>
        </MudTd>
        <MudTd Style="text-align: right;">
            <MudTooltip Text="Edit" Placement="Placement.Top">
                <MudIconButton Icon="@Icons.Material.Filled.Edit" 
                               Color="Color.Default" Size="Size.Small" />
            </MudTooltip>
            <MudTooltip Text="Delete" Placement="Placement.Top">
                <MudIconButton Icon="@Icons.Material.Filled.Delete" 
                               Color="Color.Error" Size="Size.Small" />
            </MudTooltip>
        </MudTd>
    </RowTemplate>
</MudTable>
```

---

## Loading and Error States

### Loading

```razor
@if (_isLoading)
{
    <div style="display: flex; justify-content: center; align-items: center; min-height: 200px;">
        <MudProgressCircular Indeterminate="true"/>
    </div>
}
```

### Error Alert

```razor
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <MudAlert Severity="Severity.Error" Class="mb-4">@_errorMessage</MudAlert>
}
```

---

## Button Patterns

### Primary Action

```razor
<MudButton Variant="Variant.Filled" Color="Color.Primary">Save</MudButton>
```

### Secondary/Cancel Action

```razor
<MudButton Variant="Variant.Text" Color="Color.Default">Cancel</MudButton>
```

### Destructive Action

```razor
<MudButton Variant="Variant.Filled" Color="Color.Error">Delete</MudButton>
```

### Button with Icon

```razor
<MudButton Variant="Variant.Outlined" Color="Color.Primary" 
           StartIcon="@Icons.Material.Filled.Add">
    New Item
</MudButton>
```

---

## Spacing and Layout Classes

MudBlazor utility classes used throughout:

- **Padding**: `pa-4`, `pa-3`, `pa-2` (all sides), `px-4` (horizontal), `py-2` (vertical)
- **Margin**: `mb-4`, `mt-4`, `mr-2`, `ml-2` (single sides), `my-4` (vertical)
- **Flexbox**: `d-flex`, `align-center`, `justify-center`, `gap-2`
- **Text**: `text-muted` (secondary text color)

### Common Patterns

```razor
@* Stack with row layout and centered items *@
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">

@* Grid with consistent spacing *@
<MudGrid Spacing="4">
    <MudItem xs="12" md="8"><!-- Main content --></MudItem>
    <MudItem xs="12" md="4"><!-- Sidebar --></MudItem>
</MudGrid>
```

---

## Date/Time Formatting

Use consistent date/time formatting:

```razor
@* Standard format: short date + short time *@
@item.CreatedAt.ToLocalTime().ToString("g")

@* Detailed format for version details *@
@item.CreatedAt.ToString("yyyy-MM-dd HH:mm")
```

---

## Status Chips

```razor
@* Active/Live status *@
<MudChip T="string" Color="Color.Success" Size="Size.Small">Active</MudChip>

@* Warning status *@
<MudChip T="string" Color="Color.Warning" Size="Size.Small">Pending</MudChip>

@* Error status *@
<MudChip T="string" Color="Color.Error" Size="Size.Small">Failed</MudChip>
```

---

## Navigation Sidebar Patterns

Section headers in the sidebar use gradient backgrounds with left borders:

```razor
<div class="nav-section-header adventure">
    <MudIcon Icon="@Icons.Material.Filled.AutoAwesome" Size="Size.Small"/>
    Section Name
</div>
```

Available variants: default (purple), `.adventure` (blue), `.tools` (cyan)
