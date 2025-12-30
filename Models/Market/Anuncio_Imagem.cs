using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[PrimaryKey("id_anuncio", "id_imagem_anuncio")]
[Table("Anuncio_Imagem", Schema = "market")]
public partial class Anuncio_Imagem
{
    [Key]
    public int id_anuncio { get; set; }

    [Key]
    public int id_imagem_anuncio { get; set; }

    [Precision(0)]
    public DateTime data_adicao { get; set; }

    public int? ordem { get; set; }

    public bool principal { get; set; }

    [ForeignKey("id_anuncio")]
    [InverseProperty("Anuncio_Imagem")]
    public virtual Anuncio id_anuncioNavigation { get; set; } = null!;

    [ForeignKey("id_imagem_anuncio")]
    [InverseProperty("Anuncio_Imagem")]
    public virtual Imagem_Anuncio id_imagem_anuncioNavigation { get; set; } = null!;
}
