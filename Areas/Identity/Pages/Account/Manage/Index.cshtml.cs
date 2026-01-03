#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        MarketplaceDbContext marketDb,
        CurrentUserService currentUser,
        ILogger<IndexModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _marketDb = marketDb;
        _currentUser = currentUser;
        _logger = logger;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        // Identity
        [Required]
        [StringLength(50, ErrorMessage = "O username deve ter no máximo {1} caracteres.")]
        [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "O username só pode conter letras, números, ponto, underscore e hífen.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = null!;

        // market.Utilizador
        [Required]
        [StringLength(120)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = null!;

        [Required]
        [StringLength(20)]
        [Display(Name = "Canal de notificação preferido")]
        public string CanalNotificacaoPreferido { get; set; } = "email";

        [Display(Name = "Receber novidades")]
        public bool NotificarNovidades { get; set; } = true;

        // market.Morada
        [Required]
        [StringLength(200)]
        [Display(Name = "Endereço")]
        public string Endereco { get; set; } = null!;

        [StringLength(20)]
        [Display(Name = "Número")]
        public string? Numero { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression(@"^\d{4}-\d{3}$", ErrorMessage = "Código postal inválido (formato: 1234-567).")]
        [Display(Name = "Código postal")]
        public string CodigoPostal { get; set; } = null!;

        // market.Utilizador_Contacto
        [Required(ErrorMessage = "Campo obrigatório.")]
        [StringLength(120)]
        [Display(Name = "1º contacto Telemóvel (obrigatório)")]
        public string Contacto1Valor { get; set; } = null!;

        [StringLength(120)]
        [Display(Name = "2º contacto Telemóvel/Telefone (opcional)")]
        public string? Contacto2Valor { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("Utilizador Identity não encontrado.");

        var marketId = await _currentUser.GetMarketUserIdAsync(User);
        if (marketId is null) return Forbid();

        var u = await _marketDb.Utilizador
            .Include(x => x.Morada)
            .Include(x => x.Utilizador_Contacto)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.id_utilizador == marketId.Value);

        if (u is null) return NotFound("Utilizador marketplace não encontrado.");

        var morada = u.Morada.OrderBy(m => m.id_morada).FirstOrDefault();

        var c1 = u.Utilizador_Contacto
            .Where(c => c.tipo == "telemovel")
            .OrderBy(c => c.id_contacto)
            .FirstOrDefault();

        var c2 = u.Utilizador_Contacto
            .Where(c => c1 == null || c.id_contacto != c1.id_contacto)
            .OrderBy(c => c.id_contacto)
            .FirstOrDefault();

        Input = new InputModel
        {
            Username = (await _userManager.GetUserNameAsync(user)) ?? "",
            Nome = u.nome,
            CanalNotificacaoPreferido = (u.canal_notificacao_preferido ?? "email").Trim(),
            NotificarNovidades = u.notificar_novidades,

            Endereco = morada?.endereco ?? "",
            Numero = morada?.numero,
            CodigoPostal = morada?.cod_postal ?? "",

            Contacto1Valor = c1?.valor ?? "",
            Contacto2Valor = c2?.valor
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("Utilizador Identity não encontrado.");

        var marketId = await _currentUser.GetMarketUserIdAsync(User);
        if (marketId is null) return Forbid();

        Input.Username = (Input.Username ?? "").Trim();
        Input.Nome = (Input.Nome ?? "").Trim();
        Input.CanalNotificacaoPreferido = (Input.CanalNotificacaoPreferido ?? "email").Trim().ToLowerInvariant();
        Input.Endereco = (Input.Endereco ?? "").Trim();
        Input.Numero = string.IsNullOrWhiteSpace(Input.Numero) ? null : Input.Numero.Trim();
        Input.CodigoPostal = (Input.CodigoPostal ?? "").Trim();

        var canaisValidos = new HashSet<string> { "email", "sms" };
        if (!canaisValidos.Contains(Input.CanalNotificacaoPreferido))
            ModelState.AddModelError("Input.CanalNotificacaoPreferido", "Canal inválido. Use: email ou sms.");

        var c1Digits = NormalizePtPhone(Input.Contacto1Valor);
        var c2Digits = NormalizePtPhone(Input.Contacto2Valor);

        if (string.IsNullOrWhiteSpace(c1Digits))
            ModelState.AddModelError("Input.Contacto1Valor", "O telemóvel é obrigatório.");
        else if (!IsPtMobile(c1Digits))
            ModelState.AddModelError("Input.Contacto1Valor", "Telemóvel inválido (formato PT: 9xxxxxxxx).");

        string? c2Tipo = null;
        if (!string.IsNullOrWhiteSpace(c2Digits))
        {
            if (IsPtMobile(c2Digits)) c2Tipo = "telemovel";
            else if (IsPtFixed(c2Digits)) c2Tipo = "telefone";
            else ModelState.AddModelError("Input.Contacto2Valor", "Contacto inválido (PT: 9xxxxxxxx ou 2xxxxxxxx).");
        }

        if (!string.IsNullOrWhiteSpace(c1Digits) && !string.IsNullOrWhiteSpace(c2Digits) && c1Digits == c2Digits)
            ModelState.AddModelError(string.Empty, "O 2º contacto não pode ser igual ao 1º contacto.");

        // Username: validar unicidade
        var currentUserName = await _userManager.GetUserNameAsync(user) ?? "";
        if (!string.Equals(Input.Username, currentUserName, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByNameAsync(Input.Username);
            if (existing != null && existing.Id != user.Id)
                ModelState.AddModelError("Input.Username", "Esse username já está a ser utilizado.");
        }

        if (!ModelState.IsValid)
            return Page();

        // 1) Identity username
        if (!string.Equals(Input.Username, currentUserName, StringComparison.OrdinalIgnoreCase))
        {
            var setResult = await _userManager.SetUserNameAsync(user, Input.Username);
            if (!setResult.Succeeded)
            {
                foreach (var e in setResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }
        }

        // 2) Marketplace
        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var u = await _marketDb.Utilizador
                .Include(x => x.Morada)
                .Include(x => x.Utilizador_Contacto)
                .FirstOrDefaultAsync(x => x.id_utilizador == marketId.Value);

            if (u is null) return NotFound("Utilizador marketplace não encontrado.");

            u.nome = Input.Nome;
            u.canal_notificacao_preferido = Input.CanalNotificacaoPreferido;
            u.notificar_novidades = Input.NotificarNovidades;

            // Morada (1 principal)
            var morada = u.Morada.OrderBy(m => m.id_morada).FirstOrDefault();
            if (morada is null)
            {
                _marketDb.Morada.Add(new Morada
                {
                    id_utilizador = u.id_utilizador,
                    endereco = Input.Endereco,
                    numero = Input.Numero,
                    cod_postal = Input.CodigoPostal
                });
            }
            else
            {
                morada.endereco = Input.Endereco;
                morada.numero = Input.Numero;
                morada.cod_postal = Input.CodigoPostal;
            }

            // Contactos: mantém 1 telemóvel principal + 1 opcional
            var contacts = u.Utilizador_Contacto.ToList();

            var c1Row = contacts.FirstOrDefault(c => c.tipo == "telemovel");
            if (c1Row is null)
            {
                c1Row = new Utilizador_Contacto { id_utilizador = u.id_utilizador, tipo = "telemovel", valor = c1Digits };
                _marketDb.Utilizador_Contacto.Add(c1Row);
            }
            else
            {
                c1Row.valor = c1Digits;
            }

            foreach (var extra in contacts.Where(c => c.id_contacto != c1Row.id_contacto))
                _marketDb.Utilizador_Contacto.Remove(extra);

            if (!string.IsNullOrWhiteSpace(c2Digits))
            {
                _marketDb.Utilizador_Contacto.Add(new Utilizador_Contacto
                {
                    id_utilizador = u.id_utilizador,
                    tipo = c2Tipo!,
                    valor = c2Digits
                });
            }

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Falha ao atualizar perfil marketplace.");
            ModelState.AddModelError(string.Empty, "Não foi possível guardar os teus dados. Verifica contactos/morada e tenta novamente.");
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Perfil atualizado com sucesso.";
        return RedirectToPage();
    }

    private static string NormalizePtPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("351") && digits.Length == 12) digits = digits[3..];
        return digits;
    }

    private static bool IsPtMobile(string digits9) => Regex.IsMatch(digits9, @"^9\d{8}$");
    private static bool IsPtFixed(string digits9) => Regex.IsMatch(digits9, @"^2\d{8}$");
}
