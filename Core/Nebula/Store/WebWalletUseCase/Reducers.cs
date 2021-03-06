﻿using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public static class Reducers
	{
		[ReducerMethod]
		public static WebWalletState ReduceFetchDataAction(WebWalletState state, WebWalletCreateAction action) =>
			new WebWalletState(
				IsOpeing: false,
				wallet: null,
				Stage: UIStage.Entry);

        [ReducerMethod]
		public static WebWalletState CloseAction(WebWalletState state, WebWalletCloseAction action) =>
			new WebWalletState(
				IsOpeing: false,
				wallet: null,
				Stage: UIStage.Entry);

		[ReducerMethod]
		public static WebWalletState SendAction(WebWalletState state, WebWalletSendAction action) =>
	new WebWalletState(
		IsOpeing: state.IsOpening,
		wallet: state.wallet,
		Stage: UIStage.Send);

		[ReducerMethod]
		public static WebWalletState CancelSendAction(WebWalletState state, WebWalletCancelSendAction action) =>
			new WebWalletState(
			IsOpeing: state.IsOpening,
			wallet: state.wallet,
			Stage: UIStage.Main);

		[ReducerMethod]
		public static WebWalletState ReduceFetchDataResultAction(WebWalletState state, WebWalletResultAction action) =>
			new WebWalletState(
				IsOpeing: action.IsOpening,
				wallet: action.wallet,
				Stage: UIStage.Main);
	}
}
