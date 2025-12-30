using System;
using System.Collections.Generic;
using AutoMarket.Models;
using AutoMarket.Models.Market;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Data;

public partial class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext()
    {
    }

    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ace_Admin_Acao> Ace_Admin_Acao { get; set; }

    public virtual DbSet<Administrador> Administrador { get; set; }

    public virtual DbSet<Anuncio> Anuncio { get; set; }

    public virtual DbSet<Anuncio_Imagem> Anuncio_Imagem { get; set; }

    public virtual DbSet<Candidatura_Vendedor> Candidatura_Vendedor { get; set; }

    public virtual DbSet<Comprador> Comprador { get; set; }

    public virtual DbSet<Comprador_Filtro> Comprador_Filtro { get; set; }

    public virtual DbSet<Comprador_Marca> Comprador_Marca { get; set; }

    public virtual DbSet<Encomenda> Encomenda { get; set; }

    public virtual DbSet<Filtro> Filtro { get; set; }

    public virtual DbSet<Imagem_Anuncio> Imagem_Anuncio { get; set; }

    public virtual DbSet<Informacoes_Market> Informacoes_Market { get; set; }

    public virtual DbSet<Marca> Marca { get; set; }

    public virtual DbSet<Market_Contacto> Market_Contacto { get; set; }

    public virtual DbSet<Market_Horario_Suporte> Market_Horario_Suporte { get; set; }

    public virtual DbSet<Market_Horario_Suporte_Excecao> Market_Horario_Suporte_Excecao { get; set; }

    public virtual DbSet<Modelo> Modelo { get; set; }

    public virtual DbSet<Morada> Morada { get; set; }

    public virtual DbSet<Notificacao> Notificacao { get; set; }

    public virtual DbSet<Notificacao_Recebida> Notificacao_Recebida { get; set; }

    public virtual DbSet<Publica_Atualiza> Publica_Atualiza { get; set; }

    public virtual DbSet<Reserva> Reserva { get; set; }

    // ✅ NOVO: DbSet adicionado
    public virtual DbSet<Sub_Modelo> Sub_Modelo { get; set; }

    public virtual DbSet<Utilizador> Utilizador { get; set; }

    public virtual DbSet<Utilizador_Contacto> Utilizador_Contacto { get; set; }

    public virtual DbSet<Veiculo> Veiculo { get; set; }

    public virtual DbSet<Vendedor> Vendedor { get; set; }

    public virtual DbSet<Visita> Visita { get; set; }

    public virtual DbSet<vw_Vendedor_Stats> vw_Vendedor_Stats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ace_Admin_Acao>(entity =>
        {
            entity.Property(e => e.data_acao).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.id_adminNavigation).WithMany(p => p.Ace_Admin_Acao)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AceAdmin_Admin");

            entity.HasOne(d => d.id_candidaturaNavigation).WithMany(p => p.Ace_Admin_Acao).HasConstraintName("FK_AceAdmin_Candidatura");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Ace_Admin_Acao).HasConstraintName("FK_AceAdmin_Util");
        });

        modelBuilder.Entity<Administrador>(entity =>
        {
            entity.Property(e => e.id_utilizador).ValueGeneratedNever();

            entity.HasOne(d => d.id_utilizadorNavigation).WithOne(p => p.Administrador).HasConstraintName("FK_Administrador_Utilizador");
        });

        modelBuilder.Entity<Anuncio>(entity =>
        {
            entity.Property(e => e.prazo_reserva).HasDefaultValue(168);

            entity.HasOne(d => d.id_veiculoNavigation).WithMany(p => p.Anuncio).HasConstraintName("FK_Anuncio_Veiculo");

            entity.HasOne(d => d.id_vendedorNavigation).WithMany(p => p.Anuncio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Anuncio_Vendedor");
        });

        // ✅ MANTÉM A TUA CORREÇÃO MANUAL (PK composta + WithMany)
        modelBuilder.Entity<Anuncio_Imagem>(entity =>
        {
            entity.ToTable("Anuncio_Imagem", "market");

            // PK composta correta
            entity.HasKey(e => new { e.id_anuncio, e.id_imagem_anuncio })
                  .HasName("PK_Anuncio_Imagem");

            // Índice único filtrado "principal"
            entity.HasIndex(e => e.id_anuncio, "UX_Anuncio_Imagem_Principal")
                .IsUnique()
                .HasFilter("([principal]=(1))");

            entity.Property(e => e.data_adicao).HasDefaultValueSql("(sysutcdatetime())");

            // Anuncio 1:N Anuncio_Imagem
            entity.HasOne(d => d.id_anuncioNavigation)
                .WithMany(p => p.Anuncio_Imagem)
                .HasForeignKey(d => d.id_anuncio)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AnuncioImagem_Anuncio");

            // Imagem_Anuncio 1:N Anuncio_Imagem
            entity.HasOne(d => d.id_imagem_anuncioNavigation)
                .WithMany(p => p.Anuncio_Imagem)
                .HasForeignKey(d => d.id_imagem_anuncio)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AnuncioImagem_Imagem");
        });

        modelBuilder.Entity<Candidatura_Vendedor>(entity =>
        {
            entity.HasIndex(e => e.id_utilizador, "UX_CandVend_Utilizador_Pendente")
                .IsUnique()
                .HasFilter("([estado]=N'pendente')");

            entity.Property(e => e.data_submissao).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.estado).HasDefaultValue("pendente");

            entity.HasOne(d => d.id_utilizadorNavigation).WithOne(p => p.Candidatura_Vendedor).HasConstraintName("FK_CandVend_Utilizador");
        });

        modelBuilder.Entity<Comprador>(entity =>
        {
            entity.Property(e => e.id_utilizador).ValueGeneratedNever();

            entity.HasOne(d => d.id_utilizadorNavigation).WithOne(p => p.Comprador).HasConstraintName("FK_Comprador_Utilizador");
        });

        modelBuilder.Entity<Comprador_Filtro>(entity =>
        {
            entity.Property(e => e.data_filtros).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.id_filtroNavigation).WithMany(p => p.Comprador_Filtro).HasConstraintName("FK_CompradorFiltro_Filtro");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Comprador_Filtro).HasConstraintName("FK_CompradorFiltro_Comprador");
        });

        modelBuilder.Entity<Comprador_Marca>(entity =>
        {
            entity.Property(e => e.data_subscricao).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.id_marcaNavigation).WithMany(p => p.Comprador_Marca).HasConstraintName("FK_CompradorMarca_Marca");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Comprador_Marca).HasConstraintName("FK_CompradorMarca_Comprador");
        });

        modelBuilder.Entity<Encomenda>(entity =>
        {
            entity.Property(e => e.data_encomenda).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.moeda)
                .HasDefaultValue("EUR")
                .IsFixedLength();

            entity.HasOne(d => d.id_anuncioNavigation).WithMany(p => p.Encomenda)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Encomenda_Anuncio");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Encomenda).HasConstraintName("FK_Encomenda_Comprador");
        });

        modelBuilder.Entity<Market_Contacto>(entity =>
        {
            entity.HasOne(d => d.id_marketNavigation).WithMany(p => p.Market_Contacto).HasConstraintName("FK_Market_Contacto_Info");
        });

        modelBuilder.Entity<Market_Horario_Suporte>(entity =>
        {
            entity.Property(e => e.timezone).HasDefaultValue("Europe/Lisbon");

            entity.HasOne(d => d.id_marketNavigation).WithMany(p => p.Market_Horario_Suporte).HasConstraintName("FK_Market_HorarioSuporte_Info");
        });

        modelBuilder.Entity<Market_Horario_Suporte_Excecao>(entity =>
        {
            entity.HasOne(d => d.id_marketNavigation).WithMany(p => p.Market_Horario_Suporte_Excecao).HasConstraintName("FK_Market_HorarioSuporteExc_Info");
        });

        modelBuilder.Entity<Modelo>(entity =>
        {
            entity.HasOne(d => d.id_marcaNavigation).WithMany(p => p.Modelo).HasConstraintName("FK_Modelo_Marca");
        });

        modelBuilder.Entity<Sub_Modelo>(entity =>
        {
            entity.HasOne(d => d.id_modeloNavigation)
                .WithMany(p => p.Sub_Modelo)
                .HasForeignKey(d => d.id_modelo)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SubModelo_Modelo");
        });

        modelBuilder.Entity<Morada>(entity =>
        {
            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Morada).HasConstraintName("FK_Morada_Utilizador");
        });

        modelBuilder.Entity<Notificacao_Recebida>(entity =>
        {
            entity.Property(e => e.data_envio).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.id_notificacaoNavigation).WithMany(p => p.Notificacao_Recebida).HasConstraintName("FK_NotifRec_Notif");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Notificacao_Recebida).HasConstraintName("FK_NotifRec_Util");
        });

        // ✅ MANTÉM A TUA CORREÇÃO MANUAL (WithMany para histórico)
        modelBuilder.Entity<Publica_Atualiza>(entity =>
        {
            entity.HasKey(e => e.id_acao).HasName("PK_PublicaAtualiza");

            entity.ToTable("Publica_Atualiza", "market", tb => tb.HasTrigger("trg_PublicaAtualiza_SyncAnuncio"));

            entity.HasIndex(e => e.id_anuncio, "UX_PublicaAtualiza_Criar_Unico")
                .IsUnique()
                .HasFilter("([tipo_acao]=N'criar')");

            entity.Property(e => e.data_acao).HasDefaultValueSql("(sysutcdatetime())");

            // Anuncio 1:N Publica_Atualiza (histórico)
            entity.HasOne(d => d.id_anuncioNavigation)
                .WithMany(p => p.Publica_Atualiza)
                .HasForeignKey(d => d.id_anuncio)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PublicaAtualiza_Anuncio");

            entity.HasOne(d => d.id_vendedorNavigation)
                .WithMany(p => p.Publica_Atualiza)
                .HasForeignKey(d => d.id_vendedor)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublicaAtualiza_Vendedor");
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.HasIndex(e => new { e.id_utilizador, e.id_anuncio }, "UX_Reserva_Ativa")
                .IsUnique()
                .HasFilter("([estado]=N'ativa')");

            entity.Property(e => e.data_reserva).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.id_anuncioNavigation).WithMany(p => p.Reserva).HasConstraintName("FK_Reserva_Anuncio");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Reserva).HasConstraintName("FK_Reserva_Comprador");
        });

        modelBuilder.Entity<Utilizador>(entity =>
        {
            entity.ToTable("Utilizador", "market", tb => tb.HasTrigger("trg_Utilizador_Update"));

            entity.Property(e => e.canal_notificacao_preferido).HasDefaultValue("email");
            entity.Property(e => e.data_atualizacao).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.data_criacao).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.notificar_novidades).HasDefaultValue(true);
            entity.Property(e => e.estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ativo");
        });

        // ✅ MANTÉM A TUA CORREÇÃO MANUAL (índice único)
        modelBuilder.Entity<Utilizador_Contacto>(entity =>
        {
            entity.HasOne(d => d.id_utilizadorNavigation)
                  .WithMany(p => p.Utilizador_Contacto)
                  .HasConstraintName("FK_Util_Contacto_Util");

            // Restrição: o mesmo utilizador não pode ter dois contactos com o mesmo "valor"
            entity.HasIndex(e => new { e.id_utilizador, e.valor })
                  .IsUnique()
                  .HasDatabaseName("UX_UtilizadorContacto_Utilizador_Valor");
        });

        modelBuilder.Entity<Veiculo>(entity =>
        {
            entity.HasOne(d => d.id_sub_modeloNavigation)
                .WithMany(p => p.Veiculo)
                .HasForeignKey(d => d.id_sub_modelo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Veiculo_SubModelo");
        });

        modelBuilder.Entity<Vendedor>(entity =>
        {
            entity.Property(e => e.id_utilizador).ValueGeneratedNever();

            entity.HasOne(d => d.id_candidaturaNavigation).WithOne(p => p.Vendedor)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vendedor_Candidatura");

            entity.HasOne(d => d.id_utilizadorNavigation).WithOne(p => p.Vendedor).HasConstraintName("FK_Vendedor_Utilizador");
        });

        modelBuilder.Entity<Visita>(entity =>
        {
            entity.HasOne(d => d.id_anuncioNavigation).WithMany(p => p.Visita).HasConstraintName("FK_Visita_Anuncio");

            entity.HasOne(d => d.id_utilizadorNavigation).WithMany(p => p.Visita).HasConstraintName("FK_Visita_Comprador");
        });

        modelBuilder.Entity<vw_Vendedor_Stats>(entity =>
        {
            entity.ToView("vw_Vendedor_Stats", "market");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
