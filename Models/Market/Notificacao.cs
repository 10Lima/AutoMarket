using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Notificacao", Schema = "market")]
public partial class Notificacao
{
    [Key]
    public long id_notificacao { get; set; }

    [StringLength(40)]
    public string tipo { get; set; } = null!;

    [StringLength(160)]
    public string titulo { get; set; } = null!;

    public string? texto { get; set; }

    public string? payload_json { get; set; }

    [InverseProperty("id_notificacaoNavigation")]
    public virtual ICollection<Notificacao_Recebida> Notificacao_Recebida { get; set; } = new List<Notificacao_Recebida>();
}
