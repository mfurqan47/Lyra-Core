﻿using Lyra.Core.API;
using Lyra.Core.Decentralize;
using Nebula.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.NodeViewUseCase
{
	public class NodeViewResultAction
	{
		public BillBoard billBoardResult { get; }
		public ConcurrentDictionary<string, GetSyncStateAPIResult> nodeStatusResult { get; }

		public NodeViewResultAction(BillBoard billBoard, ConcurrentDictionary<string, GetSyncStateAPIResult> NodeStatusResult)
		{
			billBoardResult = billBoard;
			nodeStatusResult = NodeStatusResult;
		}
	}
}
