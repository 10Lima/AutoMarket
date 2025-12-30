using System.Text.RegularExpressions;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminSeedController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public AdminSeedController(
        UserManager<IdentityUser> userManager,
        MarketplaceDbContext marketDb,
        CurrentUserService currentUser)
    {
        _userManager = userManager;
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    // GET: /AdminSeed/CriarCandidaturasTeste
    [HttpGet]
    public async Task<IActionResult> CriarCandidaturasTeste()
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        const int TOTAL = 20;
        const string PASSWORD = "Teste123!";

        await using var tx = await _marketDb.Database.BeginTransactionAsync();

        try
        {
            int criados = 0;

            for (int i = 1; i <= TOTAL; i++)
            {
                var username = $"seed_user_{i:00}";
                var email = $"seed_user_{i:00}@example.com";

                if (await _userManager.FindByNameAsync(username) != null)
                    continue;

                IdentityUser? identityUser = null;

                try
                {
                    // 1) Identity
                    identityUser = new IdentityUser
                    {
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(identityUser, PASSWORD);
                    if (!result.Succeeded)
                        throw new Exception(string.Join(" | ", result.Errors.Select(e => e.Description)));

                    // 2) market.Utilizador
                    var utilizador = new Utilizador
                    {
                        identity_user_id = identityUser.Id,
                        nome = $"Utilizador Seed {i:00}",
                        canal_notificacao_preferido = "email",
                        notificar_novidades = true
                    };

                    _marketDb.Utilizador.Add(utilizador);
                    await _marketDb.SaveChangesAsync();

                    // 3) Comprador (regra do teu sistema)
                    _marketDb.Comprador.Add(new Comprador
                    {
                        id_utilizador = utilizador.id_utilizador
                    });

                    // 4) Morada (obrigatória no teu fluxo)
                    _marketDb.Morada.Add(new Morada
                    {
                        id_utilizador = utilizador.id_utilizador,
                        endereco = $"Rua Seed {i:00}",
                        numero = (i % 5 == 0) ? null : (10 + i).ToString(),
                        cod_postal = GerarCodigoPostal(i) // 5000-101.. etc (formato 1234-567)
                    });

                    // 5) Contactos (1 obrigatório + 1 opcional)
                    // Contacto 1: telemovel obrigatório
                    var c1 = GerarTelemovel(i);
                    _marketDb.Utilizador_Contacto.Add(new Utilizador_Contacto
                    {
                        id_utilizador = utilizador.id_utilizador,
                        tipo = "telemovel",
                        valor = c1
                    });

                    // Contacto 2: opcional
                    // - em alguns casos cria telefone (2xxxxxxxx)
                    // - noutros telemovel (9xxxxxxxx)
                    // - noutros não cria (null)
                    var (c2Tipo, c2Valor) = GerarContacto2(i);
                    if (!string.IsNullOrWhiteSpace(c2Valor) && !string.IsNullOrWhiteSpace(c2Tipo))
                    {
                        // Evitar duplicado com o contacto 1
                        if (!string.Equals(c1, c2Valor, StringComparison.Ordinal))
                        {
                            _marketDb.Utilizador_Contacto.Add(new Utilizador_Contacto
                            {
                                id_utilizador = utilizador.id_utilizador,
                                tipo = c2Tipo,
                                valor = c2Valor
                            });
                        }
                    }

                    // 6) Candidatura
                    bool isParticular = i % 2 == 1;

                    var candidatura = new Candidatura_Vendedor
                    {
                        id_utilizador = utilizador.id_utilizador,
                        tipo = isParticular ? "particular" : "empresa",
                        NIF_NIPC = GerarNifOuNipc(isParticular),
                        telemovel_comercial = (i % 3 == 0) ? null : GerarTelemovelComercial(i),
                        website_url = (i % 4 == 0) ? null : $"https://empresa{i:00}.pt",
                        estado = "pendente"
                    };

                    _marketDb.Candidatura_Vendedor.Add(candidatura);

                    await _marketDb.SaveChangesAsync();
                    criados++;
                }
                catch
                {
                    // Se falhou algo após criar o Identity user, apaga-o para não ficar lixo
                    if (identityUser != null)
                        await _userManager.DeleteAsync(identityUser);

                    throw;
                }
            }

            await tx.CommitAsync();
            return Content($"Seed concluída: {criados} utilizadores + candidaturas criados.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Content("Erro na seed: " + ex.Message);
        }
    }

    // =========================
    // Helpers (PT)
    // =========================

    private static string GerarCodigoPostal(int i)
    {
        // Gera 4-3: 5000-101..5000-199 (formato correto)
        var sufixo = 100 + i; // 101..120
        return $"5000-{sufixo:000}";
    }

    private static string GerarTelemovel(int i)
    {
        // 9xxxxxxxx (9 dígitos)
        // exemplo: 910000001.. (mantém formato)
        var numero = 910000000 + i;
        return numero.ToString();
    }

    private static string GerarTelemovelComercial(int i)
    {
        // outro range só para variar
        var numero = 920000000 + (i * 3);
        return numero.ToString();
    }

    private static (string? tipo, string? valor) GerarContacto2(int i)
    {
        // padrões:
        // i % 3 == 0 -> não cria
        // i % 2 == 0 -> telefone (2xxxxxxxx)
        // caso contrário -> telemovel (9xxxxxxxx)
        if (i % 3 == 0)
            return (null, null);

        if (i % 2 == 0)
        {
            var fixo = 220000000 + (i * 7); // 2xxxxxxxx
            return ("telefone", fixo.ToString());
        }
        else
        {
            var mov = 930000000 + (i * 5); // 9xxxxxxxx
            return ("telemovel", mov.ToString());
        }
    }

    // =========================
    // Geração NIF / NIPC válidos
    // =========================
    private static string GerarNifOuNipc(bool particular)
    {
        var rnd = Random.Shared;

        int primeiro = particular
            ? new[] { 1, 2, 3, 4, 7 }[rnd.Next(5)]
            : new[] { 5, 6, 9 }[rnd.Next(3)];

        var digitos = new int[9];
        digitos[0] = primeiro;

        for (int i = 1; i < 8; i++)
            digitos[i] = rnd.Next(0, 10);

        int soma = 0;
        for (int i = 0; i < 8; i++)
            soma += digitos[i] * (9 - i);

        int resto = soma % 11;
        digitos[8] = resto < 2 ? 0 : 11 - resto;

        return string.Concat(digitos);
    }
}
