using AutoMarket.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoMarket.Services.Email;

public class NotificacoesEmailDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _sender;
    private readonly ILogger<NotificacoesEmailDispatcherHostedService> _logger;
    private readonly IConfiguration _cfg;

    public NotificacoesEmailDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IEmailSender sender,
        ILogger<NotificacoesEmailDispatcherHostedService> logger,
        IConfiguration cfg)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _logger = logger;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no dispatcher de notificações por email.");
            }
        }
    }

    private string MakeAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        // já absoluto
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
            return abs.ToString();

        var baseUrl = (_cfg["App:BaseUrl"] ?? "").Trim();
        baseUrl = baseUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            return url; // fallback (relativo)

        if (!url.StartsWith("/")) url = "/" + url;
        return baseUrl + url;
    }

    private (string? actionUrl, string? actionLabel) ExtractActionFromPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            string? url = null;
            string? label = null;

            if (root.TryGetProperty("action_url", out var pUrl) && pUrl.ValueKind == JsonValueKind.String)
                url = pUrl.GetString();

            if (root.TryGetProperty("action_label", out var pLabel) && pLabel.ValueKind == JsonValueKind.String)
                label = pLabel.GetString();

            return (url, label);
        }
        catch
        {
            return (null, null);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var marketDb = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
                if (nr.id_utilizadorNavigation?.notificar_novidades != true)
                {
                    nr.estado = "enviada";
                    continue;
                }

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

                // ✅ anexar CTA no email
                var (actionUrl, actionLabel) = ExtractActionFromPayload(nr.id_notificacaoNavigation.payload_json);
                if (!string.IsNullOrWhiteSpace(actionUrl))
                {
                    var abs = MakeAbsoluteUrl(actionUrl);
                    var label = string.IsNullOrWhiteSpace(actionLabel) ? "Abrir" : actionLabel.Trim();
                    body += $"\n\n{label}: {abs}";
                }

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