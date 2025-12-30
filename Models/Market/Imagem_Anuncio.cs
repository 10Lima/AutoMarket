using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Imagem_Anuncio", Schema = "market")]
public partial class Imagem_Anuncio
{
    [Key]
    public int id_imagem_anuncio { get; set; }

    [StringLength(500)]
    public string url { get; set; } = null!;

    [StringLength(200)]
    public string? legenda { get; set; }

    [InverseProperty("id_imagem_anuncioNavigation")]
    public virtual ICollection<Anuncio_Imagem> Anuncio_Imagem { get; set; } = new List<Anuncio_Imagem>();
}
