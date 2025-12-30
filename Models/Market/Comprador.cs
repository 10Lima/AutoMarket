using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Comprador", Schema = "market")]
public partial class Comprador
{
    [Key]
    public int id_utilizador { get; set; }

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Comprador_Filtro> Comprador_Filtro { get; set; } = new List<Comprador_Filtro>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Comprador_Marca> Comprador_Marca { get; set; } = new List<Comprador_Marca>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Encomenda> Encomenda { get; set; } = new List<Encomenda>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Reserva> Reserva { get; set; } = new List<Reserva>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Visita> Visita { get; set; } = new List<Visita>();

    [ForeignKey("id_utilizador")]
    [InverseProperty("Comprador")]
    public virtual Utilizador id_utilizadorNavigation { get; set; } = null!;
}
