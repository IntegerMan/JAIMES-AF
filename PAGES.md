# JAIMES AF Pages Documentation

This document provides a comprehensive inventory of all pages in the JAIMES AF application, organized by major functional section. Each page includes a description, current style guide violations, and improvement suggestions.

> [!NOTE]
> Reference `STYLE_GUIDE.md` for the canonical UI patterns and component usage. `Home.razor` and `Admin.razor` are the gold standard pages.

---

## Reusable Components

### CompactHeroSection Component

The `<CompactHeroSection>` component provides a consistent hero section for list and tool pages. Use it instead of inline hero markup.

**Location:** `JAIMES AF.Web/Components/Shared/CompactHeroSection.razor`

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `Title` | `string` | Main title displayed in the hero section (required) |
| `Subtitle` | `string?` | Text appended after item count (e.g., "in progress") |
| `SubtitleNoItems` | `string?` | Fallback subtitle when ItemCount is 0 or null |
| `ItemCount` | `int?` | Optional count to display (e.g., "5 games") |
| `ItemName` | `string` | Name for pluralization (default: "item") |
| `Icon` | `string` | Icon from `Icons.Material.Filled.*` |
| `Theme` | `HeroTheme` | Color theme (see below) |
| `ActionText` | `string?` | Text for action button (if null, button hidden) |
| `ActionHref` | `string?` | Navigation href for action button |
| `ActionIcon` | `string` | Icon for action button (default: Add) |
| `ActionColor` | `Color?` | Override button color (defaults to theme) |
| `OnActionClick` | `EventCallback` | Click handler for in-page actions |
| `ActionDisabled` | `bool` | Whether action button is disabled |
| `SubtitleContent` | `RenderFragment?` | Custom subtitle content |

**Theme Options:**

| Theme | Color | Use Case | Icon Badge Class |
|-------|-------|----------|------------------|
| `Primary` | Purple | Games | `icon-badge-primary` |
| `Secondary` | Blue | Scenarios, Agents | `icon-badge-secondary` |
| `Tertiary` | Cyan | Characters/Players | `icon-badge-tertiary` |
| `Accent` | Green | Rulesets | `icon-badge-accent` |
| `Success` | Green | Reserved (low contrast - avoid for heroes) | `icon-badge-success` |
| `Info` | Light Blue | Evaluations, Tests, Tools | `icon-badge-info` |

**Usage Examples:**

```razor
@* List page with item count and navigation action *@
<CompactHeroSection Title="Your Adventures"
                    Icon="@Icons.Material.Filled.SportsEsports"
                    Theme="CompactHeroSection.HeroTheme.Primary"
                    ItemCount="@_games?.Length"
                    ItemName="game"
                    Subtitle="in progress"
                    SubtitleNoItems="Start a new adventure"
                    ActionText="New Game"
                    ActionHref="/games/new"/>

@* Tool page with subtle hero (no action or info-only) *@
<CompactHeroSection Title="Test Evaluators"
                    Icon="@Icons.Material.Filled.Science"
                    Theme="CompactHeroSection.HeroTheme.Success"
                    Subtitle="Test AI response evaluators against sample data"
                    ActionText="View Evaluators"
                    ActionHref="/admin/evaluators"
                    ActionIcon="@Icons.Material.Filled.List"/>

@* Action page with dynamic subtitle and in-page action *@
<CompactHeroSection Title="Run Tests"
                    Icon="@Icons.Material.Filled.PlayArrow"
                    Theme="CompactHeroSection.HeroTheme.Success">
    <SubtitleContent>
        @if (CanRun)
        {
            <span>@_selectedTests.Count test(s) ready</span>
        }
        else
        {
            <span>Select items to run tests</span>
        }
    </SubtitleContent>
</CompactHeroSection>
```

---

### FormPageHeader Component

The `<FormPageHeader>` component provides a consistent header for create and edit form pages. It matches the styling of `CompactHeroSection` but is optimized for form pages (no action button, simpler layout).

**Location:** `JAIMES AF.Web/Components/Shared/FormPageHeader.razor`

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `Title` | `string` | Main title (required). For create pages: "Create New {Entity}". For edit pages: "Edit: {EntityName}" |
| `Subtitle` | `string?` | Optional subtitle text (e.g., "Define a new AI agent") |
| `Icon` | `string` | Icon from `Icons.Material.Filled.*` |
| `Theme` | `HeroTheme` | Color theme (see below) |

**Theme Options:**

| Theme | Color | Use Case | Icon Badge Class |
|-------|-------|----------|------------------|
| `Primary` | Purple | Games | `icon-badge-primary` |
| `Secondary` | Blue | Scenarios, Agents | `icon-badge-secondary` |
| `Tertiary` | Cyan | Characters/Players | `icon-badge-tertiary` |
| `Accent` | Green | Rulesets | `icon-badge-accent` |
| `Success` | Green | Evaluations, Tests | `icon-badge-success` |
| `Warning` | Orange | Locations | `icon-badge-warning` |
| `Info` | Light Blue | Tools, Info pages | `icon-badge-info` |

**Usage Examples:**

```razor
@* Create page - static title *@
<FormPageHeader Title="Create New Game"
                Icon="@Icons.Material.Filled.SportsEsports"
                Theme="FormPageHeader.HeroTheme.Primary"
                Subtitle="Start a new adventure" />

@* Edit page - dynamic title showing entity name *@
<FormPageHeader Title="@(_isLoading ? "Edit Agent" : $"Edit: {_name}")"
                Icon="@Icons.Material.Filled.SmartToy"
                Theme="FormPageHeader.HeroTheme.Secondary"
                Subtitle="Modify agent configuration" />

@* Location edit - Warning theme *@
<FormPageHeader Title="@($"Edit: {_location?.Name ?? "Location"}")"
                Icon="@Icons.Material.Filled.Place"
                Theme="FormPageHeader.HeroTheme.Warning"
                Subtitle="Modify location details" />
```

---

## Page Patterns

### List Page Pattern

Used for pages that display a collection of items in a table.

**Reference Page:** `Games.razor`

**Structure:**
1. `<CompactHeroSection>` with item count and "New" action button
2. `<MudBreadcrumbs>` below hero
3. Loading state with centered `<MudProgressCircular>`
4. Empty state using `glass-card` styling with icon and CTA
5. `<MudTable>` with `Dense="true"` and `Hover="true"`
6. Icon action buttons wrapped in `<MudTooltip Placement="Placement.Top">`

**Pages using this pattern:** Games, Scenarios, Rulesets, Players, TestCases, TestReports, Evaluators

---

### Tool Test Page Pattern

Used for pages where users configure inputs and run an action to see results.

**Reference Page:** `EvaluatorTest.razor`

**Structure:**
1. `<CompactHeroSection>` with subtle informational styling (optional link action)
2. `<MudBreadcrumbs>` below hero
3. `<MudPaper Class="pa-6" Elevation="2">` containing:
   - Brief description text
   - Nested `<MudPaper Class="pa-4" Elevation="1">` sections for form groups
   - Action button(s) below form sections
   - Results section (shown after action completes)

**Pages using this pattern:** EvaluatorTest, RulesSearchTest, ConversationSearch, ToolTest

---

### Action Page Pattern

Used for pages focused on executing a multi-selection action.

**Reference Page:** `RunTests.razor`

**Structure:**
1. `<CompactHeroSection>` with dynamic subtitle (e.g., selection count)
2. `<MudBreadcrumbs>` below hero
3. Primary action button (large, prominent, below hero)
4. Multi-column selection panels using `<MudGrid>` and `<MudPaper>`
5. Running state with progress indicator

**Pages using this pattern:** RunTests

---

### Form Page Pattern (New/Edit)

Used for creating or editing a single entity.

**Reference Pages:** NewGame, NewAgent, EditAgent, EditPlayer

**Structure:**
1. `<FormPageHeader>` with entity icon and themed styling
2. `<MudContainer MaxWidth="MaxWidth.Medium">` (or `MaxWidth.Large` for complex multi-column forms)
3. `<MudPaper Class="pa-6" Elevation="2">` containing:
   - `<MudBreadcrumbs>` with `mb-4` spacing
   - `<MudForm>` with `Variant.Outlined` fields
   - Action buttons at bottom with `mt-4` gap styling

**Button Conventions:**
- **Create pages**: Primary button uses `StartIcon="@Icons.Material.Filled.Add"` with entity-specific color
- **Edit pages**: Primary button uses `StartIcon="@Icons.Material.Filled.Check"` with entity-specific color
- **Cancel button**: Uses `Variant.Text`, `Color.Default`, and `Href` to navigate back

**Entity-Specific Button Colors:**
| Entity | Button Color |
|--------|--------------|
| Games | `Color.Primary` |
| Agents, Scenarios | `Color.Secondary` |
| Players | `Color.Tertiary` |
| Rulesets | `Color.Success` |
| Locations | `Color.Warning` |

**Usage Example:**
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

**Pages using this pattern:** NewGame, NewAgent, NewScenario, NewPlayer, NewRuleset, EditAgent, EditScenario, EditPlayer, EditRuleset, EditLocation

---

## Primary Navigation

These are the top-level pages accessible directly from the main navigation.

### [Home.razor](JAIMES%20AF.Web/Components/Pages/Home.razor)
**Route:** `/`

**Type:** Hero

![Home Page](Images/Home.png)

Landing page with premium hero section, quick action cards, feature pills, and a comprehensive stats dashboard displaying game, content, platform, and AI quality metrics.

**Style Guide Compliance:** ✅ Excellent — serves as a gold standard reference page

**Improvement Ideas:**
- None — this is a reference page for styling patterns

---

### [Admin.razor](JAIMES%20AF.Web/Components/Pages/Admin.razor)
**Route:** `/admin`

**Type:** Dashboard

![Admin Page](Images/Admin.png)

System administration dashboard featuring pipeline status panels, content overview stats, AI quality metrics, and organized navigation cards grouped by Adventure, Platform, Evaluation, and Tools/Feedback columns.

**Style Guide Compliance:** ✅ Excellent — serves as a gold standard reference page

**Improvement Ideas:**
- None — this is a reference page for styling patterns

---

### [Games.razor](JAIMES%20AF.Web/Components/Pages/Games.razor)
**Route:** `/games`

**Type:** List

Lists in-progress games with agent version, creation date, and last played timestamp. Features a compact hero section with game count badge. Games using the latest agent version display a "Latest" chip.

![Games Page](Images/Games.png)

**Style Guide Compliance:** ✅ Good - this is a gold standard reference page for lists

---

## Adventure / Content Section

Pages for managing game content including scenarios, characters, rulesets, and locations.

### [Scenarios.razor](JAIMES%20AF.Web/Components/Pages/Scenarios.razor)
**Route:** `/scenarios`

**Type:** List

Lists available game scenarios (story seeds) that can be used to start new games.

![Scenarios Page](Images/Scenarios.png)

**Style Guide Compliance:** ✅ Good

---

### [Players.razor](JAIMES%20AF.Web/Components/Pages/Players.razor)
**Route:** `/players`

**Type:** List

Lists player characters that can be used across different games. Features a compact hero section with player count, RulesetLink integration, and icon action buttons.

![Players Page](Images/Players.png)

**Style Guide Compliance:** ✅ Good - follows List Page Pattern

**Improvement Ideas:**
- Add character portrait/avatar display

---

### [Rulesets.razor](JAIMES%20AF.Web/Components/Pages/Rulesets.razor)
**Route:** `/rulesets`

**Type:** List

Lists game rulesets for managing game mechanics and rules.

![Rulesets Page](Images/Rulesets.png)

**Style Guide Compliance:** ✅ Good - follows List Page Pattern

**Improvement Ideas:**
- Add player/scenario count columns
- Add batch operations for managing multiple rulesets

---

### [Locations.razor](JAIMES%20AF.Web/Components/Pages/Locations.razor)
**Route:** `/admin/locations`

Browse and manage locations filtered by game, with nearby locations and event counts.

**Style Guide Violations:**
- ❌ Tooltip on "View Details" (line 108) missing `Placement.Top`
- ❌ Tooltip on "Edit Location" (line 113) missing `Placement.Top`

**Improvement Ideas:**
- Add `Placement.Top` to all tooltips
- Consider adding a map visualization for location relationships
- Add location type/category filtering

---

### [LocationDetails.razor](JAIMES%20AF.Web/Components/Pages/LocationDetails.razor)
**Route:** `/admin/locations/{id}`

Displays detailed information about a specific location including nearby locations and events.

**Style Guide Violations:**
- Review for tooltip placement consistency

**Improvement Ideas:**
- Add visual map of nearby location connections
- Include quick navigation to nearby locations

---

---

## Platform Section

Pages for managing AI agents, ML models, and RAG collections.

### [Agents.razor](JAIMES%20AF.Web/Components/Pages/Agents.razor)
**Route:** `/agents`

Lists AI agents with their roles and action buttons for viewing, improving prompts, testing, and editing.

**Style Guide Compliance:** ✅ Good — Uses proper tooltip placement and icon conventions

**Improvement Ideas:**
- ⚠️ Add compact hero section with agent count (following List Page Pattern from `Games.razor`)
- Add version count column
- Add last activity or creation date
- Consider adding agent status indicators

---

### [AgentDetails.razor](JAIMES%20AF.Web/Components/Pages/AgentDetails.razor)
**Route:** `/agents/{id}`

Detailed view of a specific agent including versions, metrics, and related game data.

**Style Guide Violations:**
- Review for `AgentLink` and `AgentVersionLink` component usage

**Improvement Ideas:**
- Add version history visualization
- Include performance trends chart

---

### [AgentVersionDetails.razor](JAIMES%20AF.Web/Components/Pages/AgentVersionDetails.razor)
**Route:** `/agents/{agentId}/versions/{versionId}`

Detailed view of a specific agent instruction version with its prompt content and metrics.

**Style Guide Violations:**
- Review for proper tooltip placements

**Improvement Ideas:**
- Add diff view comparing to previous version
- Include version-specific metrics charts

---

### [ClassificationModels.razor](JAIMES%20AF.Web/Components/Pages/ClassificationModels.razor)
**Route:** `/admin/classification-models`

Lists ML.NET classification models used for sentiment analysis and other ML tasks.

**Style Guide Compliance:** ✅ Good — Uses compact hero section, MudTable with action buttons, glass-card empty state, proper tooltip placement

**Improvement Ideas:**
- Add model comparison feature for side-by-side metric and confusion matrix analysis
- Add bulk delete for failed training jobs

---

### [ClassificationModelDetails.razor](JAIMES%20AF.Web/Components/Pages/ClassificationModelDetails.razor)
**Route:** `/admin/classification-models/{id}`

Detailed view of a specific ML classification model including training parameters, evaluation metrics, and confusion matrix.

**Style Guide Compliance:** ✅ Good — Uses compact hero section, stat-card metrics, overline section headers, enhanced confusion matrix with tooltips

**Improvement Ideas:**
- Add model download/export functionality
- Add re-training option with adjusted parameters

---

### [RagCollections.razor](JAIMES%20AF.Web/Components/Pages/RagCollections.razor)
**Route:** `/admin/rag-collections`

Browse and manage RAG (Retrieval Augmented Generation) vector collections for rules and transcripts.

**Style Guide Violations:**
- Review for tooltip placement consistency

**Improvement Ideas:**
- Add vector count visualization
- Include search quality metrics
- Add collection health indicators

---

---

## Verification / Quality Section

Pages for testing, evaluating, and monitoring AI quality.

### [Evaluators.razor](JAIMES%20AF.Web/Components/Pages/Evaluators.razor)
**Route:** `/admin/evaluators`

Lists registered evaluators with their aggregate metrics including pass/fail counts and average scores.

**Style Guide Violations:**
- ❌ Tooltip on "View Metrics" button (line 113) missing `Placement.Top`

**Improvement Ideas:**
- Add `Placement.Top` to all tooltips
- Add evaluator category grouping
- Include trend indicators for pass/fail rates

---

### [EvaluationMetricsList.razor](JAIMES%20AF.Web/Components/Pages/EvaluationMetricsList.razor)
**Route:** `/admin/metrics`

Displays evaluation metrics with filtering by evaluator, agent, and version.

**Style Guide Violations:**
- Review for tooltip and icon consistency

**Improvement Ideas:**
- Add metric trend visualizations
- Include score distribution charts

---

### [EvaluatorTest.razor](JAIMES%20AF.Web/Components/Pages/EvaluatorTest.razor)
**Route:** `/admin/evaluators/test`

Interactive testing interface for running evaluators against sample messages.

**Style Guide Violations:**
- Review for form layout and button patterns

**Improvement Ideas:**
- Add example message templates
- Include evaluator comparison mode

---

### [TestCases.razor](JAIMES%20AF.Web/Components/Pages/TestCases.razor)
**Route:** `/admin/test-cases`

Lists test cases for agent evaluation with run counts, status, and source agent links.

**Style Guide Compliance:** ✅ Good — Uses `AgentLink` component and proper tooltip placements

**Improvement Ideas:**
- ⚠️ Add compact hero section with test case count (following List Page Pattern from `Games.razor`)
- Add bulk selection for batch operations
- Include test case grouping/tagging
- Add last run date column

---

### [TestCaseDetails.razor](JAIMES%20AF.Web/Components/Pages/TestCaseDetails.razor)
**Route:** `/admin/test-cases/{id}`

Detailed view of a specific test case including run history and results.

**Style Guide Violations:**
- Review for tooltip consistency

**Improvement Ideas:**
- Add pass/fail trend visualization
- Include comparison with other test cases

---

### [TestReports.razor](JAIMES%20AF.Web/Components/Pages/TestReports.razor)
**Route:** `/admin/test-reports`

Lists test execution reports with agent versions, evaluator counts, and results.

**Style Guide Violations:**
- ⚠️ Apply List Page Pattern from `Games.razor` (compact hero, icon action buttons)
- Review for tooltip placements and icon usage

**Improvement Ideas:**
- Add compact hero section with report count and summary stats
- Add report comparison shortcuts
- Add report status indicators (passed/failed/mixed)

---

### [RunTests.razor](JAIMES%20AF.Web/Components/Pages/RunTests.razor)
**Route:** `/admin/run-tests`

Interface for running test cases against selected agent versions with evaluator configuration.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add preset test configurations
- Include estimated run time indicator
- Add progress feedback during long runs

---

### [TestRunComparison.razor](JAIMES%20AF.Web/Components/Pages/TestRunComparison.razor)
**Route:** `/admin/test-reports/{id}/compare`

Matrix comparison view of test results across multiple agent versions.

**Style Guide Violations:**
- Review for tooltip placements on metric displays

**Improvement Ideas:**
- Add visual highlighting for best/worst performers
- Include export to CSV/Excel functionality

---

---

## Tools and Feedback Section

Pages for tools, feedback, and sentiment analysis.

### [FeedbackList.razor](JAIMES%20AF.Web/Components/Pages/FeedbackList.razor)
**Route:** `/admin/feedback`

Lists user feedback entries with professional admin styling. Features a compact hero section with purple/cyan gradient, summary stat cards (positive, negative, with comments, positive rate) that filter the grid when clicked, and `AdminFilters` for filtering by game, agent, version, and feedback type.

**Recent Updates:**
- Added compact hero section consistent with SentimentList style
- Implemented stat card row showing feedback statistics with click-to-filter behavior
- Added loading skeleton for stats while data loads

**Improvement Ideas:**
- Add export functionality (CSV/Excel) for filtered feedback data
- Include feedback trend visualization over time

---

### [FeedbackDetails.razor](JAIMES%20AF.Web/Components/Pages/FeedbackDetails.razor)
**Route:** `/admin/feedback/{id}`

Detailed view of specific feedback including message context and response.

**Style Guide Violations:**
- Review for chat message component usage

**Improvement Ideas:**
- Add quick action buttons for common responses
- Include related feedback suggestions

---

### [SentimentList.razor](JAIMES%20AF.Web/Components/Pages/SentimentList.razor)
**Route:** `/admin/sentiments`

**Type:** List

Lists sentiment analysis results across messages. Features a compact hero section with total record count, sentiment summary stat cards (positive/neutral/negative breakdown with clickable filters), and a link to Classification Models.

**Style Guide Compliance:** ✅ Good - follows List Page Pattern with hero section, stat cards, and consistent component usage

**Improvement Ideas:**
- Add sentiment trend chart
- Add time-based filtering
- Add export functionality for sentiment data

---

### [SentimentDetails.razor](JAIMES%20AF.Web/Components/Pages/SentimentDetails.razor)
**Route:** `/admin/sentiments/{id}`

Detailed view of a specific sentiment analysis including message context.

**Style Guide Violations:**
- Review for chat message component usage

**Improvement Ideas:**
- Add comparison with similar messages
- Include ML confidence score display

---

### [ToolUsage.razor](JAIMES%20AF.Web/Components/Pages/ToolUsage.razor)
**Route:** `/admin/tools`

Displays tool call statistics and usage metrics across agents.

**Style Guide Compliance:** ✅ Good — Clean layout with proper breadcrumbs

**Improvement Ideas:**
- Add tool usage trend visualizations
- Include success/failure rate metrics
- Add tool performance comparisons

---

### [ToolDetails.razor](JAIMES%20AF.Web/Components/Pages/ToolDetails.razor)
**Route:** `/admin/tools/{toolName}`

Detailed view of a specific tool including call history and parameters.

**Style Guide Violations:**
- Review for tooltip placements

**Improvement Ideas:**
- Add parameter distribution visualization
- Include execution time histogram

---

### [ToolTestPage.razor](JAIMES%20AF.Web/Components/Pages/ToolTestPage.razor)
**Route:** `/admin/tool-test`

Interactive testing interface for agent tools.

**Style Guide Violations:**
- Review for form and button consistency

**Improvement Ideas:**
- Add preset test scenarios
- Include tool comparison mode

---

---

## Other Tools (Footer Section)

Testing and search utilities accessible from the Admin dashboard footer.

### [RulesSearchTest.razor](JAIMES%20AF.Web/Components/Pages/RulesSearchTest.razor)
**Route:** `/tools/rules-search`

Interactive search interface for testing ruleset/sourcebook RAG searches.

**Style Guide Violations:**
- Review for form and result display consistency

**Improvement Ideas:**
- Add search history
- Include relevance score visualization

---

### [ConversationSearchTest.razor](JAIMES%20AF.Web/Components/Pages/ConversationSearchTest.razor)
**Route:** `/tools/conversation-search`

Interactive search interface for testing conversation/transcript RAG searches.

**Style Guide Violations:**
- Review for form and result display consistency

**Improvement Ideas:**
- Add context window visualization
- Include conversation threading display

---

### [LocationLookupTest.razor](JAIMES%20AF.Web/Components/Pages/LocationLookupTest.razor)
**Route:** `/tools/location-lookup`

Testing interface for location lookup tool functionality.

**Style Guide Violations:**
- Review for form consistency

**Improvement Ideas:**
- Add location map visualization
- Include nearby location previews

---

### [LocationManagementTest.razor](JAIMES%20AF.Web/Components/Pages/LocationManagementTest.razor)
**Route:** `/tools/location-management`

Testing interface for location management tool functionality.

**Style Guide Violations:**
- Review for form consistency

**Improvement Ideas:**
- Add location relationship builder
- Include validation preview

---

---

## Game Flow Pages

Pages involved in the game playing experience.

### [GameDetails.razor](JAIMES%20AF.Web/Components/Pages/GameDetails.razor)
**Route:** `/games/{id}`

Main game playing interface with chat conversation, message input, and game state management.

**Style Guide Violations:**
- Review for `PlayerMessage`, `AssistantMessage`, and `ErrorMessage` component usage
- Review for `MessageIndicators` and `SentimentIcon` usage in chat footers

**Improvement Ideas:**
- Add typing indicator during AI response
- Include character portrait display
- Add quick action buttons for common player actions

---

### [NewGame.razor](JAIMES%20AF.Web/Components/Pages/NewGame.razor)
**Route:** `/games/new`

Interface for creating a new game with scenario, character, and agent selection.

**Style Guide Violations:**
- Review for form layout and select component styling

**Improvement Ideas:**
- Add scenario preview cards
- Include character/agent recommendations
- Add "Quick Start" with random selections

---

### [TranscriptMessageDetails.razor](JAIMES%20AF.Web/Components/Pages/TranscriptMessageDetails.razor)
**Route:** `/games/{gameId}/messages/{messageId}`

Detailed view of a specific message including evaluations and tool calls.

**Style Guide Violations:**
- Review for chat message component usage

**Improvement Ideas:**
- Add context window showing surrounding messages
- Include evaluation breakdown visualization

---

---

## Entity Edit Pages

Form pages for creating and editing entities.

### [EditAgent.razor](JAIMES%20AF.Web/Components/Pages/EditAgent.razor)
**Route:** `/agents/{id}/edit`

Form for editing agent instructions (creates new version).

**Style Guide Compliance:** ✅ Good — Follows form page structure pattern

**Improvement Ideas:**
- Add prompt template suggestions
- Include version comparison preview

---

### [NewAgent.razor](JAIMES%20AF.Web/Components/Pages/NewAgent.razor)
**Route:** `/agents/new`

Form for creating a new agent.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add agent type templates
- Include sample instruction prompts

---

### [EditScenario.razor](JAIMES%20AF.Web/Components/Pages/EditScenario.razor)
**Route:** `/scenarios/{id}/edit`

Form for editing scenario details.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add preview mode
- Include AI-assisted scenario refinement

---

### [NewScenario.razor](JAIMES%20AF.Web/Components/Pages/NewScenario.razor)
**Route:** `/scenarios/new`

Form for creating a new scenario.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add scenario templates by genre
- Include AI scenario generator

---

### [EditPlayer.razor](JAIMES%20AF.Web/Components/Pages/EditPlayer.razor)
**Route:** `/players/{id}/edit`

Form for editing character details.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add character portrait upload
- Include attribute builder

---

### [NewPlayer.razor](JAIMES%20AF.Web/Components/Pages/NewPlayer.razor)
**Route:** `/players/new`

Form for creating a new character.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add character templates
- Include AI character generator

---

### [EditRuleset.razor](JAIMES%20AF.Web/Components/Pages/EditRuleset.razor)
**Route:** `/rulesets/{id}/edit`

Form for editing ruleset details.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add rule section templates
- Include sourcebook linking

---

### [NewRuleset.razor](JAIMES%20AF.Web/Components/Pages/NewRuleset.razor)
**Route:** `/rulesets/new`

Form for creating a new ruleset.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add popular system templates
- Include AI ruleset builder

---

### [EditLocation.razor](JAIMES%20AF.Web/Components/Pages/EditLocation.razor)
**Route:** `/admin/locations/{id}/edit`

Form for editing location details including nearby location relationships.

**Style Guide Violations:**
- Review for form layout consistency

**Improvement Ideas:**
- Add map-based nearby location selector
- Include auto-generated descriptions

---

---

## Utility Pages

System and utility pages.

### [Error.razor](JAIMES%20AF.Web/Components/Pages/Error.razor)
**Route:** `/error`

Error display page shown when unhandled exceptions occur.

**Style Guide Violations:**
- Review for proper error styling

**Improvement Ideas:**
- Add error categorization
- Include recovery suggestions
- Add "Report Issue" functionality

---

### [PromptImproverWizard.razor](JAIMES%20AF.Web/Components/Pages/PromptImproverWizard.razor)
**Route:** `/agents/{id}/improve`

Multi-step wizard for AI-assisted prompt improvement.

**Style Guide Violations:**
- Review for wizard step consistency

**Improvement Ideas:**
- Add before/after comparison
- Include rollback functionality
- Add improvement suggestion explanations

---

### [TrainClassifier.razor](JAIMES%20AF.Web/Components/Pages/TrainClassifier.razor)
**Route:** `/admin/classification-models/{id}/train`

Interface for training ML classification models.

**Style Guide Violations:**
- Review for form and progress display consistency

**Improvement Ideas:**
- Add training progress visualization
- Include model validation metrics

---

---

## Dialog Components

Modal dialogs used across the application.

### [DiagnosticsDialog.razor](JAIMES%20AF.Web/Components/Pages/DiagnosticsDialog.razor)
Displays diagnostic information for debugging.

### [EvaluationMetricsDialog.razor](JAIMES%20AF.Web/Components/Pages/EvaluationMetricsDialog.razor)
Shows detailed evaluation metrics in a modal.

### [FeedbackDialog.razor](JAIMES%20AF.Web/Components/Pages/FeedbackDialog.razor)
Form dialog for submitting feedback.

### [NewLocationDialog.razor](JAIMES%20AF.Web/Components/Pages/NewLocationDialog.razor)
Form dialog for creating new locations.

---

## Summary of Common Style Guide Violations

| Issue | Severity | Affected Pages |
|-------|----------|----------------|
| Missing `Placement.Top` on tooltips | High | Locations, Evaluators |
| Icon buttons without tooltip wrappers | High | (Fixed in most pages) |

## Priority Improvements

1. **High Priority:** Add `Placement.Top` to all tooltips application-wide
2. **High Priority:** Wrap all icon buttons in `MudTooltip`
3. **Medium Priority:** Replace text action buttons with icon buttons in tables
4. **Low Priority:** Add visualization components to metric pages
