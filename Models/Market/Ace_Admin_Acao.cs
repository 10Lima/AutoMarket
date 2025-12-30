using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Ace_Admin_Acao", Schema = "market")]
[Index("id_admin", "data_acao", Name = "IX_AceAdmin_ByAdmin", IsDescending = new[] { false, true })]
[Index("id_utilizador", "data_acao", Name = "IX_AceAdmin_Target", IsDescending = new[] { false, true })]
public partial class Ace_Admin_Acao
{
    [Key]
    public long id_acao { get; set; }

    public int id_admin { get; set; }

    public int id_utilizador { get; set; }

    public int? id_candidatura { get; set; }

    [Precision(0)]
    public DateTime data_acao { get; set; }

    [StringLength(40)]
    public string tipo_acao { get; set; } = null!;

    [StringLength(400)]
    public string? motivo { get; set; }

    [ForeignKey("id_admin")]
    [InverseProperty("Ace_Admin_Acao")]
    public virtual Administrador id_adminNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Ace_Admin_Acao")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;

    [ForeignKey("id_candidatura")]
    [InverseProperty("Ace_Admin_Acao")]
    public virtual Candidatura_Vendedor? id_candidaturaNavigation { get; set; }

}
