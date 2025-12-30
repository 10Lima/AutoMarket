using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Encomenda", Schema = "market")]
[Index("id_anuncio", "data_encomenda", Name = "IX_Encomenda_Anuncio_Data", IsDescending = new[] { false, true })]
[Index("id_utilizador", "data_encomenda", Name = "IX_Encomenda_Comprador_Data", IsDescending = new[] { false, true })]
[Index("estado", "data_encomenda", Name = "IX_Encomenda_Estado_Data")]
public partial class Encomenda
{
    [Key]
    public long id_encomenda { get; set; }

    public int id_utilizador { get; set; }

    public int id_anuncio { get; set; }

    [Precision(0)]
    public DateTime data_encomenda { get; set; }

    [StringLength(40)]
    public string metodo_pagamento { get; set; } = null!;

    [StringLength(20)]
    public string estado { get; set; } = null!;

    [StringLength(80)]
    public string? ref_pagamento { get; set; }

    [StringLength(3)]
    [Unicode(false)]
    public string moeda { get; set; } = null!;

    [ForeignKey("id_anuncio")]
    [InverseProperty("Encomenda")]
    public virtual Anuncio id_anuncioNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Encomenda")]
    public virtual Comprador id_utilizadorNavigation { get; set; } = null!;
}
