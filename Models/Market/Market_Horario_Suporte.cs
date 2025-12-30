using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Market_Horario_Suporte", Schema = "market")]
[Index("id_market", "weekday", Name = "UQ_Market_Horario_Suporte", IsUnique = true)]
public partial class Market_Horario_Suporte
{
    [Key]
    public int id_horario_suporte { get; set; }

    public int id_market { get; set; }

    public byte weekday { get; set; }

    [Precision(0)]
    public TimeOnly? abre { get; set; }

    [Precision(0)]
    public TimeOnly? fecha { get; set; }

    public bool fechado { get; set; }

    [StringLength(64)]
    public string timezone { get; set; } = null!;

    [StringLength(200)]
    public string? observacoes { get; set; }

    [ForeignKey("id_market")]
    [InverseProperty("Market_Horario_Suporte")]
    public virtual Informacoes_Market id_marketNavigation { get; set; } = null!;
}
