using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Utilizador_Contacto", Schema = "market")]
[Index("id_utilizador", "tipo", "valor", Name = "UQ_Util_Contacto", IsUnique = true)]
[Index("id_utilizador", "valor", Name = "UX_UtilizadorContacto_Utilizador_Valor", IsUnique = true)]
public partial class Utilizador_Contacto
{
    [Key]
    public int id_contacto { get; set; }

    public int id_utilizador { get; set; }

    [StringLength(30)]
    public string tipo { get; set; } = null!;

    [StringLength(120)]
    public string valor { get; set; } = null!;

    [ForeignKey("id_utilizador")]
    [InverseProperty("Utilizador_Contacto")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
