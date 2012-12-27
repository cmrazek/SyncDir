using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SyncDirCmd
{
	class ReportWriter : IDisposable
	{
		private StreamWriter _rep = null;
		private bool _started = false;

		public ReportWriter(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException();
			_rep = new StreamWriter(stream, Encoding.ASCII);
		}

		public ReportWriter(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException();
			_rep = new StreamWriter(fileName, false, Encoding.ASCII);
		}

		public void Dispose()
		{
			Close();
		}

		public void Close()
		{
			if (_rep != null)
			{
				if (_started)
				{
					_rep.WriteLine("</body></html>");
				}
				_rep.Close();
				_rep = null;
			}
		}

		public void StartReport(string title)
		{
			_rep.WriteLine("<!DOCTYPE html>");
			_rep.WriteLine("<html>");

			_rep.WriteLine("<head>");
			if (!string.IsNullOrEmpty(title)) _rep.WriteLine(string.Format("<title>{0}</title>", HttpUtility.HtmlEncode(title)));

			_rep.WriteLine("<style type=\"text/css\">");
			_rep.WriteLine("h1 { font-family: \"Verdana\"; background-color: #6666aa; color: #ffffff; }");
			_rep.WriteLine("body { font-family: \"Verdana\"; font-size: .8em; background-color: #f8f8f8; }");
			_rep.WriteLine(".headerTable { border: 1px solid #cccccc; padding: 0px 0px 0px 0px; width: 100%; }");
			_rep.WriteLine(".headerItem { border: 1px solid #cccccc; padding: 0px 0px 0px 0px; }");
			_rep.WriteLine(".headerLabel { font-weight: bold; background-color: #6666aa; color: #ffffff; width: 200px; }");
			_rep.WriteLine(".headerValue { }");
			_rep.WriteLine(".infoItem { }");
			_rep.WriteLine(".errorItem { color: #ffffff; background-color: #ff0000; font-weight: bold; border: 1px solid #cccccc; margin-top: 4px; margin-bottom: 4px; padding-left: 4px; }");
			_rep.WriteLine(".errorException { font-family: Consolas, Courier New; background-color: #ffffff; color: #000000; }");
			_rep.WriteLine(".warningItem { color: #ffffff; background-color: #ff8800; font-weight: bold; border: 1px solid #cccccc; margin-top: 4px; margin-bottom: 4px; padding-left: 4px; }");
			_rep.WriteLine(".operationItem { font-weight: bold; border: 1px solid #cccccc; margin-top: 4px; margin-bottom: 4px; padding-left: 4px; }");
			_rep.WriteLine(".operationDetailTable { margin-left: 20px; }");
			_rep.WriteLine(".operationDetailItem { }");
			_rep.WriteLine(".operationDetailLabel { display: inline-block; font-weight: normal; width: 180px; }");
			_rep.WriteLine(".operationDetailValue { font-weight: normal; }");
			_rep.WriteLine(".data { font-family: Consolas, Courier New; background-color: #ffffff; }");
			_rep.WriteLine(".summaryTable { border: 1px solid #cccccc; padding: 0px 0px 0px 0px; width: 100%; }");
			_rep.WriteLine(".summaryItem { border: 1px solid #cccccc; padding: 0px 0px 0px 0px; }");
			_rep.WriteLine(".summaryLabel { font-weight: bold; background-color: #6666aa; color: #ffffff; width: 200px; }");
			_rep.WriteLine(".summaryValue { }");
			_rep.WriteLine("</style>");

			_rep.WriteLine("</head>");

			_rep.WriteLine("<body>");
			if (!string.IsNullOrEmpty(title)) _rep.WriteLine(string.Format("<h1>{0}</h1>", HttpUtility.HtmlEncode(title)));
			
			_started = true;
		}

		public void StartHeader()
		{
			_rep.WriteLine("<table class=\"headerTable\">");
		}

		public void WriteHeader(string label, string value)
		{
			_rep.Write("<tr class=\"headerItem\"><td class=\"headerLabel\">");
			_rep.Write(HttpUtility.HtmlEncode(label));
			_rep.Write("</td><td class=\"headerValue data\">");
			_rep.Write(HttpUtility.HtmlEncode(value));
			_rep.WriteLine("</td></tr>");
		}

		public void EndHeader()
		{
			_rep.WriteLine("</table>");
		}

		public void WriteInfo(string message)
		{
			_rep.Write("<div class=\"infoItem\">");
			_rep.Write(HttpUtility.HtmlEncode(message));
			_rep.WriteLine("</div>");
		}

		public void WriteError(Exception ex, string message)
		{
			_rep.Write("<div class=\"errorItem\">");
			if (!string.IsNullOrWhiteSpace(message))
			{
				_rep.Write(HttpUtility.HtmlEncode(message));
				_rep.Write("<br/>");
			}
			_rep.Write("<span class=\"errorException\">");
			_rep.Write(HttpUtility.HtmlEncode(ex.ToString()));
			_rep.WriteLine("</span></div>");
		}

		public void WriteWarning(string message)
		{
			_rep.Write("<div class=\"warningItem\">");
			_rep.Write(HttpUtility.HtmlEncode(message));
			_rep.WriteLine("</div>");
		}

		public void WriteOperation(string message, params ReportDetail[] details)
		{
			_rep.Write("<div class=\"operationItem\">");
			_rep.Write(HttpUtility.HtmlEncode(message));
			if (details != null && details.Length > 0)
			{
				_rep.Write("<table class=\"operationDetailTable\">");
				foreach (var d in details)
				{
					_rep.Write("<tr class=\"operationDetailItem\"><td class=\"operationDetailLabel\">");
					_rep.Write(HttpUtility.HtmlEncode(d.Label));
					_rep.Write("</td><td class=\"operationDetailValue data\">");
					_rep.Write(HttpUtility.HtmlEncode(d.Value));
					_rep.Write("</td></tr>");
				}
				_rep.Write("</table>");
			}
			_rep.WriteLine("</div>");
		}

		public void StartSummary()
		{
			_rep.WriteLine("<table class=\"summaryTable\">");
		}

		public void WriteSummary(string label, string value)
		{
			_rep.Write("<tr class=\"summaryItem\"><td class=\"summaryLabel\">");
			_rep.Write(HttpUtility.HtmlEncode(label));
			_rep.Write("</td><td class=\"summaryValue data\">");
			_rep.Write(HttpUtility.HtmlEncode(value));
			_rep.WriteLine("</td></tr>");
		}

		public void EndSummary()
		{
			_rep.WriteLine("</table>");
		}
	}
}
