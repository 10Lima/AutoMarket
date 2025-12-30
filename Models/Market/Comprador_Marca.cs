using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[PrimaryKey("id_utilizador", "id_marca")]
[Table("Comprador_Marca", Schema = "market")]
public partial class Comprador_Marca
{
    [Key]
    public int id_utilizador { get; set; }

    [Key]
    public int id_marca { get; set; }

    [Precision(0)]
    public DateTime data_subscricao { get; set; }

    [ForeignKey("id_marca")]
    [InverseProperty("Comprador_Marca")]
    public virtual Marca id_marcaNavigation { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Comprador_Marca")]
    public virtual Comprador id_utilizadorNavigation { get; set; } = null!;
}
