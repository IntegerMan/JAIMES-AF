using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class FeedbackDialog : ComponentBase
{
    [Parameter] public int MessageId { get; set; }
    [Parameter] public bool? PreSelectedFeedback { get; set; }
    [Parameter] public EventCallback<GameDetails.MessageFeedbackInfo?> OnFeedbackSubmitted { get; set; }
    
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
        // Dialog will be closed by parent when result is canceled
    }

    private async Task Submit()
    {
        if (_selectedFeedback.HasValue)
        {
            var feedbackInfo = new GameDetails.MessageFeedbackInfo
            {
                MessageId = MessageId,
                IsPositive = _selectedFeedback.Value,
                Comment = _comment
            };
            await OnFeedbackSubmitted.InvokeAsync(feedbackInfo);
            // Signal to parent that we're done - parent will close the dialog
        }
    }
}

