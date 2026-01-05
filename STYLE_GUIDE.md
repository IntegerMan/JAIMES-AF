# JAIMES AF UI Style Guide

This document defines the UI conventions, patterns, and reusable components used throughout the JAIMES AF Blazor application. It is intended to help AI agents and developers maintain consistency when creating or modifying UI code.

**Primary Reference Pages**: `Home.razor` and `Admin.razor` are the gold standard for styling conventions. The sidebar (`NavMenu.razor`) demonstrates navigation patterns.

---

## Quick Reference

When building UI, follow these patterns:

- **Displaying an agent name** → Use `<AgentLink>` component
- **Displaying agent + version together** → Use `<AgentVersionDisplay>` component
- **Displaying a version number alone** → Use `<AgentVersionLink>` component
- **Icon buttons** → Always wrap in `<MudTooltip Placement="Placement.Top">`
- **Tooltips** → Always use `Placement.Top` for better visibility
- **Agent/bot icon** → `Icons.Material.Filled.SmartToy` with `Color.Secondary`
- **Page titles** → Use `Typo.h4` inside a `MudPaper` container
- **Section labels** → Use `Typo.overline` with purple color and letter-spacing
- **Chat messages** → Use the `PlayerMessage`, `AssistantMessage`, or `ErrorMessage` components

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

| Component | Purpose |
|-----------|---------|
| `AgentLink.razor` | Displays agent name as a linked chip with bot icon (`SmartToy`). Use whenever showing an agent name. |
| `AgentVersionLink.razor` | Displays version number as a linked chip with update icon. Use for standalone version references. |
| `AgentVersionDisplay.razor` | Combined agent name + version number display. Use when showing both together. |
| `CompactLinkCard.razor` | Navigation card with colored icon badge. Use for dashboard/home page links. |
| `SentimentIcon.razor` | Sentiment indicator (thumbs up/down/neutral) with optional edit menu. Use in chat footers. |
| `MessageIndicators.razor` | Displays evaluation metrics and tool call badges. Use in assistant message footers. |
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
| Location | `Icons.Material.Filled.Place` | `Color.Warning` |
| Version/Update | `Icons.Material.Filled.Update` | `Color.Info` |

### Action Icons

| Action | Icon | Typical Color |
|--------|------|---------------|
| View/Details | `Icons.Material.Filled.Visibility` | `Color.Primary` |
| Edit | `Icons.Material.Filled.Edit` | `Color.Default` |
| Delete | `Icons.Material.Filled.Delete` | `Color.Error` |
| Refresh | `Icons.Material.Filled.Refresh` | `Color.Default` |
| Improve/AI | `Icons.Material.Filled.AutoAwesome` | `Color.Secondary` |
| Test/Evaluate | `Icons.Material.Filled.Science` | `Color.Success` |
| Download/Export | `Icons.Material.Filled.Download` | `Color.Info` |
| Run/Play | `Icons.Material.Filled.PlayArrow` | `Color.Success` |
| Save/Confirm | `Icons.Material.Filled.Check` | `Color.Success` |
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

- ✅ All icon buttons (required)
- ✅ Chips that link to other pages
- ✅ Status indicators
- ✅ Any element whose purpose isn't immediately obvious
- ❌ Text buttons with clear labels (tooltip is redundant)

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

```razor
<MudContainer MaxWidth="MaxWidth.Medium" Class="mt-4">
    <MudPaper Class="pa-4">
        <MudBreadcrumbs Items="_breadcrumbs" Separator=">" Class="mb-2"/>
        <MudText Typo="Typo.h4" Class="mb-4">Edit Entity</MudText>

        <MudForm>
            <MudTextField @bind-Value="_name" Label="Name" 
                          Variant="Variant.Outlined" Class="mb-4" />
            
            <div class="d-flex gap-2">
                <MudButton Variant="Variant.Filled" Color="Color.Primary">Save</MudButton>
                <MudButton Variant="Variant.Text" Color="Color.Default">Cancel</MudButton>
            </div>
        </MudForm>
    </MudPaper>
</MudContainer>
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

### Section Headers

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

### Stat Card

Used for metrics/statistics with colored left border:

```razor
<div class="stat-card stat-card-primary">
    <MudText Typo="Typo.caption">Label</MudText>
    <MudText Typo="Typo.h6">Value</MudText>
</div>
```

Available variants: `stat-card-primary`, `stat-card-secondary`, `stat-card-tertiary`, `stat-card-accent`, `stat-card-warning`, `stat-card-error`

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
