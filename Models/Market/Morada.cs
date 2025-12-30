using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Morada", Schema = "market")]
public partial class Morada
{
    [Key]
    public int id_morada { get; set; }

    public int id_utilizador { get; set; }

    [StringLength(200)]
    public string endereco { get; set; } = null!;

    [StringLength(20)]
    public string? numero { get; set; }

    [StringLength(20)]
    public string cod_postal { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Morada")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
