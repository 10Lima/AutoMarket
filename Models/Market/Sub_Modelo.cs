using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoMarket.Models.Market;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models;

[Table("Sub_Modelo", Schema = "market")]
[Index("categoria", "combustivel", "caixa", Name = "IX_SubModelo_Filtros")]
[Index("id_modelo", "nome", Name = "UQ_SubModelo", IsUnique = true)]
public partial class Sub_Modelo
{
    [Key]
    public int id_sub_modelo { get; set; }

    public int id_modelo { get; set; }

    [StringLength(120)]
    public string nome { get; set; } = null!;

    [StringLength(30)]
    public string combustivel { get; set; } = null!;

    [StringLength(30)]
    public string caixa { get; set; } = null!;

    [StringLength(40)]
    public string categoria { get; set; } = null!;

    public int potencia_hp { get; set; }

    public int cilindrada { get; set; }

    [InverseProperty("id_sub_modeloNavigation")]
    public virtual ICollection<Veiculo> Veiculo { get; set; } = new List<Veiculo>();

    [ForeignKey("id_modelo")]
    [InverseProperty("Sub_Modelo")]
    public virtual Modelo id_modeloNavigation { get; set; } = null!;
}
