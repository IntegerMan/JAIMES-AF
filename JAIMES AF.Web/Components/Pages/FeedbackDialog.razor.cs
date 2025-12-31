using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class FeedbackDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public int MessageId { get; set; }
    [Parameter] public bool? PreSelectedFeedback { get; set; }
    [Parameter] public EventCallback<FeedbackSubmission?> OnFeedbackSubmitted { get; set; }

    private bool? _selectedFeedback;
    private string? _comment;

    protected override void OnParametersSet()
    {
        if (PreSelectedFeedback.HasValue)
        {
            _selectedFeedback = PreSelectedFeedback.Value;
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private async Task Submit()
    {
        if (_selectedFeedback.HasValue)
        {
            var submission = new FeedbackSubmission(MessageId, _selectedFeedback.Value, _comment);
            await OnFeedbackSubmitted.InvokeAsync(submission);
            // Signal to parent that we're done - parent will close the dialog
        }
    }
}

public record FeedbackSubmission(int MessageId, bool IsPositive, string? Comment);

