using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Informacoes_Market", Schema = "market")]
public partial class Informacoes_Market
{
    [Key]
    public int id_market { get; set; }

    [StringLength(255)]
    public string URL { get; set; } = null!;

    [StringLength(120)]
    public string nome { get; set; } = null!;

    public string termos_e_politicas { get; set; } = null!;

    [InverseProperty("id_marketNavigation")]
    public virtual ICollection<Market_Contacto> Market_Contacto { get; set; } = new List<Market_Contacto>();

    [InverseProperty("id_marketNavigation")]
    public virtual ICollection<Market_Horario_Suporte> Market_Horario_Suporte { get; set; } = new List<Market_Horario_Suporte>();

    [InverseProperty("id_marketNavigation")]
    public virtual ICollection<Market_Horario_Suporte_Excecao> Market_Horario_Suporte_Excecao { get; set; } = new List<Market_Horario_Suporte_Excecao>();
}
