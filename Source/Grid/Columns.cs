﻿using DynamicGrid.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicGrid.Data {

	public class Column {
		public int Width;
		public HorizontalAlignment Alignment;
      public Color? BackColor;
      public Color? ForeColor;
      public FontStyle FontStyle;
		public Font Font;

      public Column () {
         Alignment = HorizontalAlignment.Left;
         FontStyle = FontStyle.Regular;
      }

      public Column (int width, HorizontalAlignment alignment=HorizontalAlignment.Left) {
			Width = width;
			Alignment = alignment;
         FontStyle = FontStyle.Regular;
      }

      internal Column (InternalColumn c) {
			Width = c.Width;
			Alignment = c.Alignment;
         BackColor = c.BackColor;
         ForeColor = c.ForeColor;
         FontStyle = FontStyle.Regular;
         Font = c.Font;
      }
   }


   public class UpdateableColumns : List<Column>, IDisposable {
      private readonly RawGrid parent;
      private bool disposed;

		internal UpdateableColumns (RawGrid parent, List<InternalColumn> _columns) {
			this.parent = parent;
			foreach (var ci in _columns) Add (new Column (ci));
		}

      public void Dispose () {
			if (disposed) throw new Exception ("Already disposed!");
			parent.CreateInternalColumns (this);
         disposed = true;
		}
	}

	internal class InternalColumn
	{
		public readonly RawGrid Parent;
		public readonly int Width;
		public readonly int GlobalOffset;

      public readonly Font? Font;
      public readonly FontStyle FontStyle;
      public readonly Color? ForeColor;
      public readonly Color? BackColor;
		public readonly HorizontalAlignment Alignment;
      public int GlobalOffsetPlusWidth => GlobalOffset + Width;

		public Color EffectiveBackColor => BackColor ?? Parent.BackColor;

      public Color EffectiveForeColor => BackColor ?? Parent.ForeColor;
      public Font EffectiveFont => Font ?? Parent.Font;


      public InternalColumn (RawGrid parent, Column c, int offset) {
         Parent = parent;
         Width = c.Width;
         GlobalOffset = offset;
			Font = c.Font;
			FontStyle = c.FontStyle;
			ForeColor = c.ForeColor;
			BackColor = c.BackColor;
			Alignment = c.Alignment;
      }
   }

}