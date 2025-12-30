using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Reserva", Schema = "market")]
[Index("id_utilizador", "data_reserva", Name = "IX_Reserva_Comprador_Data", IsDescending = new[] { false, true })]
public partial class Reserva
{
    [Key]
    public long id_reserva { get; set; }

    public int id_utilizador { get; set; }

    public int id_anuncio { get; set; }

    [Precision(0)]
    public DateTime data_reserva { get; set; }

    [Precision(0)]
    public DateTime? data_expiracao { get; set; }

    [StringLength(20)]
    public string estado { get; set; } = null!;

    [ForeignKey("id_anuncio")]
    [InverseProperty("Reserva")]
    public virtual Anuncio id_anuncioNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Reserva")]
    public virtual Comprador id_utilizadorNavigation { get; set; } = null!;
}
