using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Market_Horario_Suporte_Excecao", Schema = "market")]
[Index("id_market", "data", Name = "UQ_Market_Horario_Suporte_Excecao", IsUnique = true)]
public partial class Market_Horario_Suporte_Excecao
{
    [Key]
    public int id_excecao_suporte { get; set; }

    public int id_market { get; set; }

    public DateOnly data { get; set; }

    [Precision(0)]
    public TimeOnly? abre { get; set; }

    [Precision(0)]
    public TimeOnly? fecha { get; set; }

    public bool fechado { get; set; }

    [StringLength(200)]
    public string? motivo { get; set; }

    [ForeignKey("id_market")]
    [InverseProperty("Market_Horario_Suporte_Excecao")]
    public virtual Informacoes_Market id_marketNavigation { get; set; } = null!;
}
