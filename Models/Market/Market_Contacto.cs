using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Market_Contacto", Schema = "market")]
[Index("id_market", "tipo", "valor", Name = "UQ_Market_Contacto", IsUnique = true)]
public partial class Market_Contacto
{
    [Key]
    public int id_contacto { get; set; }

    public int id_market { get; set; }

    [StringLength(30)]
    public string tipo { get; set; } = null!;

    [StringLength(120)]
    public string valor { get; set; } = null!;

    [ForeignKey("id_market")]
    [InverseProperty("Market_Contacto")]
    public virtual Informacoes_Market id_marketNavigation { get; set; } = null!;
}
