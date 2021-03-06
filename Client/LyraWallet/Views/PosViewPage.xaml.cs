﻿using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PosViewPage : ContentPage
    {
        public PosViewPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            model.ThePage = this;
            if (model.CartItems.Count == 0)
                model.LoadItemsCommand.Execute(null);
        }

        public void DeleteClicked(object sender, EventArgs e)
        {
            var item = (Xamarin.Forms.Button)sender;
            model.RemoveProductCommand.Execute((int)item.CommandParameter);
        }

        async void EditProduct_Clicked(object sender, EventArgs e)
        {
            model.ProductEditMode = !model.ProductEditMode;
            model.AddCartMode = !model.ProductEditMode;
            if (model.ProductEditMode)
            {
                tbEdit.Text = "X";
                model.EditorSize = 80;
            }
            else
            {
                tbEdit.Text = "+";
                model.EditorSize = 0;
            }
            model.LoadItemsCommand.Execute(null);
        }
    }
}