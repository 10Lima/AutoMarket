using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Visita", Schema = "market")]
[Index("id_anuncio", "data_visita", Name = "IX_Visita_Anuncio_Data", IsDescending = new[] { false, true })]
[Index("id_utilizador", "data_visita", Name = "IX_Visita_Comprador_Data", IsDescending = new[] { false, true })]
public partial class Visita
{
    [Key]
    public long id_visita { get; set; }

    public int id_utilizador { get; set; }

    public int id_anuncio { get; set; }

    [Precision(0)]
    public DateTime data_visita { get; set; }

    [StringLength(20)]
    public string estado { get; set; } = null!;

    [ForeignKey("id_anuncio")]
    [InverseProperty("Visita")]
    public virtual Anuncio id_anuncioNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Visita")]
    public virtual Comprador id_utilizadorNavigation { get; set; } = null!;
}
