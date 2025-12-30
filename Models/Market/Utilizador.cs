using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Table("Utilizador", Schema = "market")]
[Index("identity_user_id", Name = "UQ_Utilizador_identity_user", IsUnique = true)]
public partial class Utilizador
{
    [Key]
    public int id_utilizador { get; set; }

    public string identity_user_id { get; set; } = null!;

    [Precision(0)]
    public DateTime data_criacao { get; set; }

    [Precision(0)]
    public DateTime data_atualizacao { get; set; }

    [StringLength(120)]
    public string nome { get; set; } = null!;

    [StringLength(20)]
    public string canal_notificacao_preferido { get; set; } = null!;

    public bool notificar_novidades { get; set; }

    public string estado { get; set; } = "ativo";


    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Ace_Admin_Acao> Ace_Admin_Acao { get; set; } = new List<Ace_Admin_Acao>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual Administrador? Administrador { get; set; }

    [InverseProperty("id_utilizadorNavigation")]
    public virtual Comprador? Comprador { get; set; }

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Morada> Morada { get; set; } = new List<Morada>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Notificacao_Recebida> Notificacao_Recebida { get; set; } = new List<Notificacao_Recebida>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual ICollection<Utilizador_Contacto> Utilizador_Contacto { get; set; } = new List<Utilizador_Contacto>();

    [InverseProperty("id_utilizadorNavigation")]
    public virtual Candidatura_Vendedor? Candidatura_Vendedor { get; set; }

    [InverseProperty("id_utilizadorNavigation")]
    public virtual Vendedor? Vendedor { get; set; }

}
