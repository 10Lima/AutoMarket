using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[PrimaryKey("id_utilizador", "id_filtro")]
[Table("Comprador_Filtro", Schema = "market")]
public partial class Comprador_Filtro
{
    [Key]
    public int id_utilizador { get; set; }

    [Key]
    public int id_filtro { get; set; }

    [Precision(0)]
    public DateTime data_filtros { get; set; }

    [ForeignKey("id_filtro")]
    [InverseProperty("Comprador_Filtro")]
    public virtual Filtro id_filtroNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Comprador_Filtro")]
    public virtual Comprador id_utilizadorNavigation { get; set; } = null!;
}
