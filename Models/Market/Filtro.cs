using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Filtro", Schema = "market")]
public partial class Filtro
{
    [Key]
    public int id_filtro { get; set; }

    public string parametros { get; set; } = null!;

    [InverseProperty("id_filtroNavigation")]
    public virtual ICollection<Comprador_Filtro> Comprador_Filtro { get; set; } = new List<Comprador_Filtro>();
}
