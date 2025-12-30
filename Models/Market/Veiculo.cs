using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Veiculo", Schema = "market")]
public partial class Veiculo
{
    [Key]
    public int id_veiculo { get; set; }

    public int id_sub_modelo { get; set; }

    public short? ano { get; set; }
    public int? quilometragem { get; set; }
    public string? cor { get; set; }

    [InverseProperty("id_veiculoNavigation")]
    public virtual ICollection<Anuncio> Anuncio { get; set; } = new List<Anuncio>();

    [ForeignKey("id_sub_modelo")]
    [InverseProperty("Veiculo")]
    public virtual Sub_Modelo id_sub_modeloNavigation { get; set; } = null!;

}
