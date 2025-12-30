using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Models.Market;

[Keyless]
public partial class vw_Vendedor_Stats
{
    public int id_vendedor { get; set; }

    public int? anuncios_total { get; set; }

    public int? anuncios_ativos { get; set; }

    public int? anuncios_vendidos { get; set; }

    [Precision(0)]
    public DateTime? ultima_atualizacao { get; set; }
}
