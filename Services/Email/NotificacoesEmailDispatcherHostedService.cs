using AutoMarket.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Services.Email;

public class NotificacoesEmailDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _sender;
    private readonly ILogger<NotificacoesEmailDispatcherHostedService> _logger;

    public NotificacoesEmailDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IEmailSender sender,
        ILogger<NotificacoesEmailDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Verifica a cada 30 segundos (ajusta como quiseres)
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no dispatcher de notificações por email.");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var marketDb = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Busca top 20 pendentes (para não sobrecarregar)
        var pendentes = await marketDb.Notificacao_Recebida
            .Include(nr => nr.id_notificacaoNavigation)
            .Include(nr => nr.id_utilizadorNavigation)
            .Where(nr => nr.estado == "pendente" && nr.canal == "email")
            .OrderBy(nr => nr.data_envio)
            .Take(20)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        foreach (var nr in pendentes)
        {
            try
            {
                // ✅ se o user desligou "receber novidades", não enviar email
                if (nr.id_utilizadorNavigation?.notificar_novidades != true)
                {
                    nr.estado = "enviada"; // fica na view, mas não tenta enviar mais
                    continue;
                }

                // Email vem do Identity (AspNetUsers)
                var identityId = nr.id_utilizadorNavigation.identity_user_id;
                var user = await appDb.Users
                    .Where(u => u.Id == identityId)
                    .Select(u => new { u.Email })
                    .FirstOrDefaultAsync(ct);

                var toEmail = user?.Email;
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    nr.estado = "erro";
                    continue;
                }

                var subject = nr.id_notificacaoNavigation.titulo;
                var body = nr.id_notificacaoNavigation.texto 
                    ?? nr.id_notificacaoNavigation.titulo;

                await _sender.SendAsync(toEmail, subject, body, ct);

                nr.estado = "enviada";
                _logger.LogInformation("Email enviado: {Email} (id_recebida={Id})", toEmail, nr.id_recebida);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha a enviar email (id_recebida={Id}).", nr.id_recebida);
                nr.estado = "erro";
            }
        }

        await marketDb.SaveChangesAsync(ct);
    }
}