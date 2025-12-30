using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Marca", Schema = "market")]
[Index("nome", Name = "UQ_Marca_nome", IsUnique = true)]
public partial class Marca
{
    [Key]
    public int id_marca { get; set; }

    [StringLength(100)]
    public string nome { get; set; } = null!;

    [InverseProperty("id_marcaNavigation")]
    public virtual ICollection<Comprador_Marca> Comprador_Marca { get; set; } = new List<Comprador_Marca>();

    [InverseProperty("id_marcaNavigation")]
    public virtual ICollection<Modelo> Modelo { get; set; } = new List<Modelo>();
}
