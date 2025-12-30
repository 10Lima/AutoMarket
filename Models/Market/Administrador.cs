using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Administrador", Schema = "market")]
public partial class Administrador
{
    [Key]
    public int id_utilizador { get; set; }

    [InverseProperty("id_adminNavigation")]
    public virtual ICollection<Ace_Admin_Acao> Ace_Admin_Acao { get; set; } = new List<Ace_Admin_Acao>();

    [ForeignKey("id_utilizador")]
    [InverseProperty("Administrador")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
