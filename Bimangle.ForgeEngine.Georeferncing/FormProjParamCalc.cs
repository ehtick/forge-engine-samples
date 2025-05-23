﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Bimangle.ForgeEngine.Georeferncing.Utility;

namespace Bimangle.ForgeEngine.Georeferncing
{
    partial class FormProjParamCalc : Form
    {
        private static State _LastState;

        private readonly IGeoreferncingHost _Host;

        public FormProjParamCalc(IGeoreferncingHost host) : this()
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public FormProjParamCalc()
        {
            InitializeComponent();
        }

        public double LocalY { get; private set; }
        public double LocalX { get; private set; }
        public double Lat { get; private set; }
        public double Lon { get; private set; }

        private void FormProjParamCalc_Load(object sender, EventArgs e)
        {
            txtRefPointLocalY.Text = _LastState?.RefPointLocalY ?? @"0";
            txtRefPointLocalX.Text = _LastState?.RefPointLocalX ?? @"0";
            txtRefPointGeoLat.Text = _LastState?.RefPointGeoLat ?? string.Empty;
            txtRefPointGeoLon.Text = _LastState?.RefPointGeoLon ?? string.Empty;

            _Host.Adapter.SetDirectionLetters(lblRefPointLocalX, lblRefPointLocalY, null);
        }

        private void FormProjParamCalc_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void FormProjParamCalc_Activated(object sender, EventArgs e)
        {
            {
                const string FORMAT = @"BimAngle/GeographicPosition";

                var data = Clipboard.GetData(FORMAT);
                if (data != null)
                {
                    Clipboard.SetData(FORMAT, null);

                    var s = data.ToString();
                    var values = s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length >= 2)
                    {
                        if (double.TryParse(values[0], out var lat) &&
                            double.TryParse(values[1], out var lon))
                        {
                            txtRefPointGeoLat.Text = lat.ToLatLonString();
                            txtRefPointGeoLon.Text = lon.ToLatLonString();

                            txtRefPointGeoLat.TryParseLatitude(errorProvider1, out _);
                            txtRefPointGeoLon.TryParseLongitude(errorProvider1, out _);
                        }
                    }
                }
            }

            {
                //const string FORMAT = @"BimAngle/RevitPosition";

                var data = Clipboard.GetText(); //.GetData(FORMAT);
                if (string.IsNullOrEmpty(data) == false)
                {
                    //Clipboard.SetData(FORMAT, null);

                    var values = data.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length >= 2)
                    {
                        if (double.TryParse(values[0], out var east) &&
                            double.TryParse(values[1], out var north))
                        {
                            txtRefPointLocalX.Text = east.ToMetreString();
                            txtRefPointLocalY.Text = north.ToMetreString();

                            txtRefPointLocalX.TryParseNumber(errorProvider1, out _);
                            txtRefPointLocalY.TryParseNumber(errorProvider1, out _);
                        }
                    }
                }
            }
        }

        private void txtRefPointLocalY_Validating(object sender, CancelEventArgs e)
        {
            txtRefPointLocalY.TryParseNumber(errorProvider1, out _);
        }

        private void txtRefPointLocalX_Validating(object sender, CancelEventArgs e)
        {
            txtRefPointLocalX.TryParseNumber(errorProvider1, out _);
        }

        private void txtRefPointGeoLat_Validating(object sender, CancelEventArgs e)
        {
            txtRefPointGeoLat.TryParseLatitude(errorProvider1, out _);
        }

        private void txtRefPointGeoLon_Validating(object sender, CancelEventArgs e)
        {
            txtRefPointGeoLon.TryParseLongitude(errorProvider1, out _);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (txtRefPointLocalY.TryParseNumber(errorProvider1, out var localY) == false)
            {
                txtRefPointLocalY.Focus();
                return;
            }

            if (txtRefPointLocalX.TryParseNumber(errorProvider1, out var localX) == false)
            {
                txtRefPointLocalX.Focus();
                return;
            }

            if (txtRefPointGeoLat.TryParseLatitude(errorProvider1, out var lat) == false)
            {
                txtRefPointGeoLat.Focus();
                return;
            }

            if (txtRefPointGeoLon.TryParseLongitude(errorProvider1, out var lon) == false)
            {
                txtRefPointGeoLon.Focus();
                return;
            }

            LocalY = localY;
            LocalX = localX;
            Lat = lat;
            Lon = lon;

            _LastState = new State
            {
                RefPointLocalY = txtRefPointLocalY.Text,
                RefPointLocalX = txtRefPointLocalX.Text,
                RefPointGeoLat = txtRefPointGeoLat.Text,
                RefPointGeoLon = txtRefPointGeoLon.Text
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnPickPosition_Click(object sender, EventArgs e)
        {
            _Host.ShowPickPositionDialog();
        }

        private void btnPickPositionOnModel_Click(object sender, EventArgs e)
        {
        }

        private class State
        {
            public string RefPointLocalY { get; set; }
            public string RefPointLocalX { get; set; }
            public string RefPointGeoLat { get; set; }
            public string RefPointGeoLon { get; set; }
        }
    }
}
