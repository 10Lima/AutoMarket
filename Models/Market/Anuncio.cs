using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Anuncio", Schema = "market")]
[Index("estado", "localizacao", "data_publicacao", "preco", Name = "IX_Anuncio_BuscaPublica")]
[Index("localizacao", Name = "IX_Anuncio_Localizacao")]
[Index("id_veiculo", Name = "IX_Anuncio_Veiculo")]
[Index("id_vendedor", "estado", Name = "IX_Anuncio_Vendedor")]
public partial class Anuncio
{
    [Key]
    public int id_anuncio { get; set; }

    public int id_vendedor { get; set; }

    public int id_veiculo { get; set; }

    [StringLength(160)]
    public string? titulo { get; set; }

    public string? descricao { get; set; }

    [StringLength(120)]
    public string? localizacao { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? preco { get; set; }

    [StringLength(20)]
    public string estado { get; set; } = null!;

    public bool destaque { get; set; }

    [Precision(0)]
    public DateTime? data_publicacao { get; set; }

    [Precision(0)]
    public DateTime? data_atualizacao { get; set; }

    public int prazo_reserva { get; set; }

    [InverseProperty("id_anuncioNavigation")]
    public virtual ICollection<Anuncio_Imagem> Anuncio_Imagem { get; set; } = new List<Anuncio_Imagem>();

    [InverseProperty("id_anuncioNavigation")]
    public virtual ICollection<Encomenda> Encomenda { get; set; } = new List<Encomenda>();

    [InverseProperty("id_anuncioNavigation")]
    public virtual ICollection<Publica_Atualiza> Publica_Atualiza { get; set; } = new List<Publica_Atualiza>();

    [InverseProperty("id_anuncioNavigation")]
    public virtual ICollection<Reserva> Reserva { get; set; } = new List<Reserva>();

    [InverseProperty("id_anuncioNavigation")]
    public virtual ICollection<Visita> Visita { get; set; } = new List<Visita>();

    [ForeignKey("id_veiculo")]
    [InverseProperty("Anuncio")]
    public virtual Veiculo id_veiculoNavigation { get; set; } = null!;

    [ForeignKey("id_vendedor")]
    [InverseProperty("Anuncio")]
    public virtual Vendedor id_vendedorNavigation { get; set; } = null!;
}
