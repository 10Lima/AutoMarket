using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Candidatura_Vendedor", Schema = "market")]
public partial class Candidatura_Vendedor
{
    [Key]
    public int id_candidatura { get; set; }

    public int id_utilizador { get; set; }

    [StringLength(20)]
    public string tipo { get; set; } = null!;

    [StringLength(20)]
    public string NIF_NIPC { get; set; } = null!;

    [StringLength(30)]
    public string? telemovel_comercial { get; set; }

    [StringLength(255)]
    public string? website_url { get; set; }

    [StringLength(20)]
    public string estado { get; set; } = null!;

    [Precision(0)]
    public DateTime data_submissao { get; set; }

    [InverseProperty("id_candidaturaNavigation")]
    public virtual ICollection<Ace_Admin_Acao> Ace_Admin_Acao { get; set; } = new List<Ace_Admin_Acao>();

    [InverseProperty("id_candidaturaNavigation")]
    public virtual Vendedor? Vendedor { get; set; }

    [ForeignKey("id_utilizador")]
    [InverseProperty("Candidatura_Vendedor")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
