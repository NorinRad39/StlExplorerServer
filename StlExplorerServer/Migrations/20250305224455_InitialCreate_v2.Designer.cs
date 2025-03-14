﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StlExplorerServer.Data;

#nullable disable

namespace StlExplorerServer.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250305224455_InitialCreate_v2")]
    partial class InitialCreate_v2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.13")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("ClassLibStlExploServ.Famille", b =>
                {
                    b.Property<int>("FamilleID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("FamilleID"));

                    b.Property<string>("NomFamille")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("FamilleID");

                    b.ToTable("Famille");
                });

            modelBuilder.Entity("ClassLibStlExploServ.Packet", b =>
                {
                    b.Property<int>("PacketID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("PacketID"));

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("SujetID")
                        .HasColumnType("int");

                    b.HasKey("PacketID");

                    b.HasIndex("SujetID");

                    b.ToTable("Packets");
                });

            modelBuilder.Entity("ClassLibStlExploServ.Sujet", b =>
                {
                    b.Property<int>("SujetID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("SujetID"));

                    b.Property<int>("FamilleID")
                        .HasColumnType("int");

                    b.Property<string>("NomSujet")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("SujetID");

                    b.HasIndex("FamilleID");

                    b.ToTable("Sujet");
                });

            modelBuilder.Entity("ClassLibStlExploServ.Packet", b =>
                {
                    b.HasOne("ClassLibStlExploServ.Sujet", "Sujet")
                        .WithMany()
                        .HasForeignKey("SujetID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Sujet");
                });

            modelBuilder.Entity("ClassLibStlExploServ.Sujet", b =>
                {
                    b.HasOne("ClassLibStlExploServ.Famille", "Famille")
                        .WithMany()
                        .HasForeignKey("FamilleID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Famille");
                });
#pragma warning restore 612, 618
        }
    }
}
