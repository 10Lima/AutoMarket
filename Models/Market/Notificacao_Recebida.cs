using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Notificacao_Recebida", Schema = "market")]
[Index("id_utilizador", "data_envio", Name = "IX_NotifRec_Util_Data", IsDescending = new[] { false, true })]
public partial class Notificacao_Recebida
{
    [Key]
    public long id_recebida { get; set; }

    public int id_utilizador { get; set; }

    public long id_notificacao { get; set; }

    [Precision(0)]
    public DateTime data_envio { get; set; }

    [StringLength(20)]
    public string canal { get; set; } = null!;

    [StringLength(20)]
    public string estado { get; set; } = null!;

    [ForeignKey("id_notificacao")]
    [InverseProperty("Notificacao_Recebida")]
    public virtual Notificacao id_notificacaoNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Notificacao_Recebida")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
