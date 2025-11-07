using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
 [Inject]
 public HttpClient Http { get; set; } = null!;

 [Inject]
 public ILoggerFactory LoggerFactory { get; set; } = null!;

 [Parameter]
 public Guid GameId { get; set; }

 private GameStateResponse? game;
 private bool isLoading = true;
 private string? errorMessage;

 protected override async Task OnParametersSetAsync()
 {
 await LoadGameAsync();
 }

 private async Task LoadGameAsync()
 {
 isLoading = true;
 errorMessage = null;
 try
 {
 game = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
 }
 catch (Exception ex)
 {
 LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to load game from API");
 errorMessage = "Failed to load game: " + ex.Message;
 }
 finally
 {
 isLoading = false;
 StateHasChanged();
 }
 }
}
