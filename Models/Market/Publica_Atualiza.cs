using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Publica_Atualiza", Schema = "market")]
[Index("id_anuncio", "data_acao", Name = "IX_PublicaAtualiza_Anuncio_Data", IsDescending = new[] { false, true })]
[Index("id_vendedor", "data_acao", Name = "IX_PublicaAtualiza_Vendedor_Data", IsDescending = new[] { false, true })]
public partial class Publica_Atualiza
{
    [Key]
    public long id_acao { get; set; }

    public int id_vendedor { get; set; }

    public int id_anuncio { get; set; }

    [StringLength(10)]
    public string tipo_acao { get; set; } = null!;

    [Precision(0)]
    public DateTime data_acao { get; set; }

    [Precision(0)]
    public DateTime? data_publicacao { get; set; }

    [Precision(0)]
    public DateTime? data_atualizacao { get; set; }

    [ForeignKey("id_anuncio")]
    [InverseProperty("Publica_Atualiza")]
    public virtual Anuncio id_anuncioNavigation { get; set; } = null!;

    [ForeignKey("id_vendedor")]
    [InverseProperty("Publica_Atualiza")]
    public virtual Vendedor id_vendedorNavigation { get; set; } = null!;
}
