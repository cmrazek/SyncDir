using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	class ReportDetail
	{
		public string Label { get; set; }
		public string Value { get; set; }

		public ReportDetail()
		{
		}

		public ReportDetail(string label, string value)
		{
			Label = label;
			Value = value;
		}
	}
}
