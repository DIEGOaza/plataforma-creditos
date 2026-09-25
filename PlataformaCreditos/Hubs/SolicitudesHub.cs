using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    public static string GrupoUsuario(string usuarioId) => $"usuario:{usuarioId}";

    public override async Task OnConnectedAsync()
    {
        var usuarioId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.UserIdentifier;
        // La conexión solo pertenece al grupo de su usuario autenticado.
        if (!string.IsNullOrWhiteSpace(usuarioId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoUsuario(usuarioId));
        }

        await base.OnConnectedAsync();
    }
}
