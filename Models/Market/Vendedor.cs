using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Vendedor", Schema = "market")]
[Index("id_candidatura", Name = "UQ_Vendedor_id_candidatura", IsUnique = true)]

public partial class Vendedor
{
    [Key]
    public int id_utilizador { get; set; }

    public int id_candidatura { get; set; }

    [InverseProperty("id_vendedorNavigation")]
    public virtual ICollection<Anuncio> Anuncio { get; set; } = new List<Anuncio>();

    [InverseProperty("id_vendedorNavigation")]
    public virtual ICollection<Publica_Atualiza> Publica_Atualiza { get; set; } = new List<Publica_Atualiza>();

    [ForeignKey("id_utilizador")]
    [InverseProperty("Vendedor")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;

    [ForeignKey("id_candidatura")]
    [InverseProperty("Vendedor")]
    public virtual Candidatura_Vendedor id_candidaturaNavigation { get; set; } = null!;

}
