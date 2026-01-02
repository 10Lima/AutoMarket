#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoMarket.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender; // ✅ Identity UI sender
        private readonly MarketplaceDbContext _marketDb;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            MarketplaceDbContext marketDb)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _marketDb = marketDb;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public bool IsAdminRegistration { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            // Identity
            [Required]
            [StringLength(50, ErrorMessage = "O username deve ter no máximo {1} caracteres.")]
            [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "O username só pode conter letras, números, ponto, underscore e hífen.")]
            [Display(Name = "Username")]
            public string Username { get; set; } = null!;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = null!;

            [Required]
            [StringLength(100, ErrorMessage = "A password deve ter entre {2} e {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = null!;

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar password")]
            [Compare("Password", ErrorMessage = "A password e a confirmação não coincidem.")]
            public string ConfirmPassword { get; set; } = null!;

            // Marketplace
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

            // Morada
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

            // Contactos
            [Required(ErrorMessage = "Campo obrigatório.")]
            [StringLength(120)]
            [Display(Name = "1º contacto Telemóvel (obrigatório)")]
            public string Contacto1Valor { get; set; } = null!;

            [StringLength(120)]
            [Display(Name = "2º contacto Telemóvel/Telefone (opcional)")]
            public string? Contacto2Valor { get; set; }
        }

        // ✅ Suporta ?admin=true E ?mode=admin (o teu link atual usa mode)
        public async Task OnGetAsync(string? returnUrl = null, bool admin = false, string? mode = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var isAdminMode = admin || string.Equals(mode, "admin", StringComparison.OrdinalIgnoreCase);
            IsAdminRegistration = isAdminMode;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null, bool admin = false, string? mode = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var isAdminMode = admin || string.Equals(mode, "admin", StringComparison.OrdinalIgnoreCase);
            IsAdminRegistration = isAdminMode;

            // Se for registo de admin, confirmar que quem está a submeter é Admin no marketplace
            int? currentAdminIdUtilizador = null;
            if (isAdminMode)
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                    return Forbid();

                var currentIdentityId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(currentIdentityId))
                    return Forbid();

                currentAdminIdUtilizador = await _marketDb.Administrador
                    .Where(a => a.id_utilizadorNavigation.identity_user_id == currentIdentityId)
                    .Select(a => (int?)a.id_utilizador)
                    .FirstOrDefaultAsync();

                if (currentAdminIdUtilizador is null)
                    return Forbid();
            }

            // Canal: só email ou sms
            var canal = (Input.CanalNotificacaoPreferido ?? "").Trim().ToLowerInvariant();
            var canaisValidos = new HashSet<string> { "email", "sms" };
            if (!canaisValidos.Contains(canal))
                ModelState.AddModelError("Input.CanalNotificacaoPreferido", "Canal inválido. Use: email ou sms.");

            if (string.IsNullOrWhiteSpace(Input.Nome))
                ModelState.AddModelError("Input.Nome", "O nome é obrigatório.");

            if (string.IsNullOrWhiteSpace(Input.Endereco))
                ModelState.AddModelError("Input.Endereco", "O endereço é obrigatório.");

            // Username: verificação de duplicado (o Identity também valida, isto é só UX)
            if (!string.IsNullOrWhiteSpace(Input.Username))
            {
                var existing = await _userManager.FindByNameAsync(Input.Username.Trim());
                if (existing != null)
                    ModelState.AddModelError("Input.Username", "Esse username já está a ser utilizado.");
            }

            // Normalizar contactos (aceita +351, espaços, hífens, etc.)
            var c1Digits = NormalizePtPhone(Input.Contacto1Valor);
            var c2Digits = NormalizePtPhone(Input.Contacto2Valor);

            // Contacto 1: obrigatório e TEM de ser telemóvel (9xxxxxxxx)
            if (string.IsNullOrWhiteSpace(c1Digits))
            {
                ModelState.AddModelError("Input.Contacto1Valor", "O telemóvel é obrigatório.");
            }
            else if (!IsPtMobile(c1Digits))
            {
                ModelState.AddModelError("Input.Contacto1Valor", "Telemóvel inválido (formato PT: 9xxxxxxxx).");
            }

            // Contacto 2: opcional, mas se preenchido tem de ser telemóvel OU telefone fixo
            string? c2Tipo = null; // "telemovel" ou "telefone"
            if (!string.IsNullOrWhiteSpace(c2Digits))
            {
                if (IsPtMobile(c2Digits))
                    c2Tipo = "telemovel";
                else if (IsPtFixed(c2Digits))
                    c2Tipo = "telefone";
                else
                    ModelState.AddModelError("Input.Contacto2Valor", "Contacto inválido (PT: 9xxxxxxxx ou 2xxxxxxxx).");
            }

            // Não permitir duplicado entre os dois
            if (!string.IsNullOrWhiteSpace(c1Digits) && !string.IsNullOrWhiteSpace(c2Digits) &&
                string.Equals(c1Digits, c2Digits, StringComparison.Ordinal))
            {
                ModelState.AddModelError(string.Empty, "O 2º contacto não pode ser igual ao 1º contacto.");
            }

            if (!ModelState.IsValid)
                return Page();

            // 1) Criar IdentityUser
            var user = CreateUser();

            await _userStore.SetUserNameAsync(user, Input.Username.Trim(), CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email.Trim(), CancellationToken.None);

            var identityResult = await _userManager.CreateAsync(user, Input.Password);
            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            // 2) Criar registos no marketplace (transação EF)
            await using var tx = await _marketDb.Database.BeginTransactionAsync();
            try
            {
                var utilizador = new Utilizador
                {
                    identity_user_id = user.Id,
                    nome = Input.Nome.Trim(),
                    canal_notificacao_preferido = canal,
                    notificar_novidades = Input.NotificarNovidades
                };

                _marketDb.Utilizador.Add(utilizador);
                await _marketDb.SaveChangesAsync();

                if (isAdminMode)
                {
                    // Registo de admins: cria Administrador e NÃO cria Comprador
                    _marketDb.Administrador.Add(new Administrador
                    {
                        id_utilizador = utilizador.id_utilizador
                    });

                    // Auditoria: admin atual criou este admin
                    var motivoAdmin = "Criação de administrador via registo (admin=true)";
                    _marketDb.Ace_Admin_Acao.Add(new Ace_Admin_Acao
                    {
                        id_admin = currentAdminIdUtilizador!.Value,
                        id_utilizador = utilizador.id_utilizador,
                        tipo_acao = "criar_admin",
                        motivo = motivoAdmin
                    });

                    // Notificação ao novo admin (admin_criado) — sempre na view; email só se notificar_novidades && canal=email
                    var payload = new
                    {
                        v = 1,
                        origem = "registo_admin",
                        id_admin_criador = currentAdminIdUtilizador!.Value
                    };

                    var notif = new Notificacao
                    {
                        tipo = "admin_criado",
                        titulo = "Agora és Administrador",
                        texto = "Foste adicionado como Administrador no site AutoMarket.",
                        payload_json = JsonSerializer.Serialize(payload)
                    };

                    var estadoEnvio = Notif_EstadoEnvio(utilizador.notificar_novidades, canal);

                    _marketDb.Notificacao_Recebida.Add(new Notificacao_Recebida
                    {
                        id_utilizador = utilizador.id_utilizador,
                        id_notificacaoNavigation = notif,
                        data_envio = DateTime.UtcNow,
                        canal = canal,
                        estado = estadoEnvio
                    });
                }
                else
                {
                    _marketDb.Comprador.Add(new Comprador
                    {
                        id_utilizador = utilizador.id_utilizador
                    });
                }

                _marketDb.Morada.Add(new Morada
                {
                    id_utilizador = utilizador.id_utilizador,
                    endereco = Input.Endereco.Trim(),
                    numero = string.IsNullOrWhiteSpace(Input.Numero) ? null : Input.Numero.Trim(),
                    cod_postal = Input.CodigoPostal.Trim()
                });

                _marketDb.Utilizador_Contacto.Add(new Utilizador_Contacto
                {
                    id_utilizador = utilizador.id_utilizador,
                    tipo = "telemovel",
                    valor = c1Digits
                });

                if (!string.IsNullOrWhiteSpace(c2Digits))
                {
                    _marketDb.Utilizador_Contacto.Add(new Utilizador_Contacto
                    {
                        id_utilizador = utilizador.id_utilizador,
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
                _logger.LogError(ex, "Falha ao criar registos no marketplace durante o registo.");
                await _userManager.DeleteAsync(user);

                ModelState.AddModelError(string.Empty, "Não foi possível concluir o registo. Tente novamente.");
                return Page();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Erro inesperado durante o registo (marketplace).");
                await _userManager.DeleteAsync(user);

                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao concluir o registo. Tente novamente.");
                return Page();
            }

            // ✅ 3) Email de confirmação (se RequireConfirmedAccount=true)
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code, returnUrl },
                    protocol: Request.Scheme);

                // Mensagem curta (pode ser HTML; o teu sender pode mandar como texto simples)
                var subject = "Confirmar email — AutoMarket";
                var message =
                    $"Confirma a tua conta AutoMarket abrindo este link:\n{callbackUrl}\n\n" +
                    "Se não foste tu, ignora este email.";

                await _emailSender.SendEmailAsync(Input.Email.Trim(), subject, message);
            }
            catch (Exception ex)
            {
                // Não bloqueia o registo; mas fica logado para veres o erro.
                _logger.LogWarning(ex, "Falha a enviar email de confirmação (IdentityUserId={Id}).", user.Id);
            }

            _logger.LogInformation(isAdminMode
                ? "Administrador registado com sucesso (Identity + marketplace)."
                : "Utilizador registado com sucesso (Identity + marketplace).");

            // ✅ Se RequireConfirmedAccount=true, não assina sessão automaticamente.
            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                // Se for admin a criar admin, volta ao register admin (para criar mais), mas avisa que foi enviado email
                if (isAdminMode)
                {
                    StatusMessage = "Administrador registado. Foi enviado um email para confirmar a conta.";
                    return RedirectToPage("./Register", new { mode = "admin" });
                }

                return RedirectToPage("RegisterConfirmation", new { email = Input.Email.Trim(), returnUrl });
            }

            // MUITO IMPORTANTE:
            // Se for um admin a criar outra conta admin, não queremos trocar a sessão atual.
            if (isAdminMode)
            {
                StatusMessage = "Administrador registado com sucesso.";
                return RedirectToPage("./Register", new { mode = "admin" });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        private static string Notif_EstadoEnvio(bool notificarNovidades, string canal)
            => (notificarNovidades && canal == "email") ? "pendente" : "enviada";

        private static string NormalizePtPhone(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var digits = new string(input.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("351") && digits.Length == 12)
                digits = digits.Substring(3);

            return digits;
        }

        private static bool IsPtMobile(string digits9) => Regex.IsMatch(digits9, @"^9\d{8}$");
        private static bool IsPtFixed(string digits9) => Regex.IsMatch(digits9, @"^2\d{8}$");

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Não foi possível criar instância de '{nameof(IdentityUser)}'.");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("O UI default requer um user store com suporte a email.");

            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
