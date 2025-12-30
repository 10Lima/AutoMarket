using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Modelo", Schema = "market")]
[Index("id_marca", "nome", Name = "UQ_Modelo", IsUnique = true)]
public partial class Modelo
{
    [Key]
    public int id_modelo { get; set; }

    public int id_marca { get; set; }

    [StringLength(120)]
    public string nome { get; set; } = null!;

    [InverseProperty("id_modeloNavigation")]
    public virtual ICollection<Sub_Modelo> Sub_Modelo { get; set; } = new List<Sub_Modelo>();

    [ForeignKey("id_marca")]
    [InverseProperty("Modelo")]
    public virtual Marca id_marcaNavigation { get; set; } = null!;
}
