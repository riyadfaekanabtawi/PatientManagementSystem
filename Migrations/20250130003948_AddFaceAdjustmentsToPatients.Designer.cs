﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PatientManagementSystem.Data;

#nullable disable

namespace PatientManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250130003948_AddFaceAdjustmentsToPatients")]
    partial class AddFaceAdjustmentsToPatients
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("PatientManagementSystem.Models.Admin", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("Admins");
                });

            modelBuilder.Entity("PatientManagementSystem.Models.Appointment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("AppointmentDateTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("BackImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("FrontImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("LeftImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("Notes")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<int>("PatientId")
                        .HasColumnType("integer");

                    b.Property<string>("RightImageUrl")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("PatientId");

                    b.ToTable("Appointments");
                });

            modelBuilder.Entity("PatientManagementSystem.Models.FaceAdjustmentHistory", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AdjustedImageUrl")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("AdjustmentDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("PatientId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("PatientId");

                    b.ToTable("FaceAdjustmentHistories");
                });

            modelBuilder.Entity("PatientManagementSystem.Models.Patient", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("BackImageUrl")
                        .HasColumnType("text");

                    b.Property<int?>("CheekAdjustment")
                        .HasColumnType("integer");

                    b.Property<int?>("ChinAdjustment")
                        .HasColumnType("integer");

                    b.Property<string>("Contact")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("DateOfBirth")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("FrontImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("LeftImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("Model3DUrl")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int?>("NoseAdjustment")
                        .HasColumnType("integer");

                    b.Property<string>("RightImageUrl")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Patients");
                });

            modelBuilder.Entity("PatientManagementSystem.Models.Appointment", b =>
                {
                    b.HasOne("PatientManagementSystem.Models.Patient", "Patient")
                        .WithMany()
                        .HasForeignKey("PatientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Patient");
                });

            modelBuilder.Entity("PatientManagementSystem.Models.FaceAdjustmentHistory", b =>
                {
                    b.HasOne("PatientManagementSystem.Models.Patient", null)
                        .WithMany("AdjustmentHistory")
                        .HasForeignKey("PatientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PatientManagementSystem.Models.Patient", b =>
                {
                    b.Navigation("AdjustmentHistory");
                });
#pragma warning restore 612, 618
        }
    }
}
