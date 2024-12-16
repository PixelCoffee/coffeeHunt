class AggregateInventoryInterface
{
	int tickOffset = 0;
	static int offseti = 0;
	public AggregateInventoryInterface()
	{
		tickOffset = offseti;
		offseti += 10;
	}
	public void setContainers<T>(List<T> c) where T : IMyTerminalBlock
	{
		containers.Clear();
		foreach (T i in c)
		{
			containers.Add(i);
		}
	}
	List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
	public Dictionary<MyItemType, int> items = new Dictionary<MyItemType, int>();

	bool itemsUpdating = false;
	Dictionary<MyItemType, int> itemsUpdate = new Dictionary<MyItemType, int>();
	int nextContainer = 0;

	public bool hasUpdatedOnce = false;

	public int updateInterval = 60 * 10;
	public int lastUpdateTick = 0;//-60*60;
								  //if called every tick will update the inventory manifest every 15 seconds since last update completed.
								  //may not be necessary - a force update is best before doing anything important.
	public void update(bool force = false)
	{
		if (!itemsUpdating && (tick + tickOffset - lastUpdateTick > updateInterval || force))
		{
			itemsUpdating = true;
			itemsUpdate = new Dictionary<MyItemType, int>();
			nextContainer = 0;
		}

		if (itemsUpdating)
		{
			if (nextContainer >= containers.Count)
			{
				itemsUpdating = false;
				lastUpdateTick = tick;
				items = itemsUpdate;
				nextContainer = 0;
				hasUpdatedOnce = true;
			}
			else
			{
				IMyTerminalBlock t = containers[nextContainer];
				for (int i = 0; i < t.InventoryCount; i++)
				{
					var inv = t.GetInventory(i);
					List<MyInventoryItem> t_items = new List<MyInventoryItem>();
					inv.GetItems(t_items);
					foreach (MyInventoryItem item in t_items)
					{
						if (!itemsUpdate.ContainsKey(item.Type)) itemsUpdate[item.Type] = (int)Math.Floor((double)item.Amount);
						else itemsUpdate[item.Type] += (int)Math.Floor((double)item.Amount);
					}
				}
				nextContainer++;
			}
		}
	}
	public int TransferItemTo(MyItemType type, int amount_to_transfer, IMyInventory destination)
	{
		float unit_volume = type.GetItemInfo().Volume;
		foreach (IMyTerminalBlock t in containers)
		{
			for (int i = 0; i < t.InventoryCount; i++)
			{
				var inv = t.GetInventory(i);
				List<MyInventoryItem> t_items = new List<MyInventoryItem>();
				inv.GetItems(t_items);
				foreach (MyInventoryItem item in t_items)
				{
					if (item.Type == type)
					{
						int transfer_amt = (int)Math.Floor((double)item.Amount);

						int max_room_for = (int)Math.Floor((double)(destination.MaxVolume - destination.CurrentVolume + (MyFixedPoint)0.001) / unit_volume);

						if (max_room_for < transfer_amt) transfer_amt = max_room_for;

						if (transfer_amt > amount_to_transfer) transfer_amt = amount_to_transfer;
						//log(">sending " + transfer_amt + " of " + item.Type.SubtypeId);


						if (inv.TransferItemTo(destination, item, transfer_amt)) amount_to_transfer -= transfer_amt;
						if (amount_to_transfer <= 0) return 0;
					}
				}
			}
		}
		return amount_to_transfer;
	}
	//these return the amount of items that could not be sent (unavailable, no room, whatever). Ergo, 0 means all were transferred.
	public int TransferItemTo(MyItemType type, int amount, AggregateInventoryInterface destination)
	{
		return TransferItemTo(type, amount, destination.containers);
	}
	public int TransferItemTo(MyItemType type, int amount, List<IMyTerminalBlock> containers)
	{
		foreach (IMyTerminalBlock t in containers)
		{
			for (int i = 0; i < t.InventoryCount; i++)
			{

				var inv = t.GetInventory(i);
				//log("sending " + amount + " of " + type.SubtypeId + " to " + t.DefinitionDisplayNameText);
				amount = TransferItemTo(type, amount, inv);
				if (amount <= 0) return amount;
			}
		}
		return amount;
	}
}

public static string v2ss(Vector3D v)
{
	return "<" + v.X.ToString("0.0000") + "," + v.Y.ToString("0.0000") + "," + v.Z.ToString("0.0000") + ">";
}

static int dd_tick;

static Vector3D dd_dfv, dd_duv;

static MatrixD dd_wm;

static double dd_y, dd_p, dd_r;

public static void GetRotationAnglesSimultaneousDedup(Vector3D fwd, Vector3D up, MatrixD wm, out double yaw, out double pitch, out double roll)
{
	if (tick != dd_tick || fwd != dd_dfv || up != dd_duv || dd_wm != wm)
	{
		dd_tick = tick;
		dd_dfv = fwd;
		dd_duv = up;
		dd_wm = wm;
		GetRotationAnglesSimultaneous(dd_dfv, dd_duv, dd_wm, out dd_y, out dd_p, out dd_r);
	}
	yaw = dd_y;
	pitch = dd_p;
	roll = dd_r;
}


/// <summary>
/// Whip's GetRotationAnglesSimultaneous - Last modified: 07/05/2020
/// </summary>
public static void GetRotationAnglesSimultaneous(Vector3D a, Vector3D b, MatrixD w, out double yaw, out double pitch, out double roll)
{
	a = SafeNormalize(a);
	MatrixD twm;
	MatrixD.Transpose(ref w, out twm);
	Vector3D.Rotate(ref a, ref twm, out a);
	Vector3D.Rotate(ref b, ref twm, out b);
	Vector3D lv = Vector3D.Cross(b, a);
	Vector3D axis;
	double angle;
	if (Vector3D.IsZero(b) || Vector3D.IsZero(lv))
	{
		axis = new Vector3D(a.Y, -a.X, 0);
		angle = Math.Acos(MathHelper.Clamp(-a.Z, -1.0, 1.0));
	}
	else
	{
		lv = SafeNormalize(lv);
		Vector3D upVector = Vector3D.Cross(a, lv);
		MatrixD tm = MatrixD.Zero;
		tm.Forward = a;
		tm.Left = lv;
		tm.Up = upVector;
		axis = new Vector3D(tm.M23 - tm.M32, tm.M31 - tm.M13, tm.M12 - tm.M21);
		double trace = tm.M11 + tm.M22 + tm.M33;
		angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1, 1));
	}
	if (Vector3D.IsZero(axis))
	{
		angle = a.Z < 0 ? 0 : Math.PI;
		yaw = angle;
		pitch = 0;
		roll = 0;
		return;
	}
	axis = SafeNormalize(axis);
	yaw = -axis.Y * angle;
	pitch = axis.X * angle;
	roll = -axis.Z * angle;
}


public static Vector3D SafeNormalize(Vector3D a)
{
	if (Vector3D.IsZero(a))
		return Vector3D.Zero;

	if (Vector3D.IsUnit(ref a))
		return a;

	return Vector3D.Normalize(a);
}

public const double d180bypi = (180 / Math.PI);

public static double ConvertRadiansToDegrees(double radians)
{
	double degrees = d180bypi * radians;
	return (degrees);
}

static double dpiby180 = (Math.PI / 180);

public static double ConvertDegreesToRadians(double degrees)
{
	double radians = dpiby180 * degrees;
	return (radians);
}

public static double cpa_time(Vector3D Tr1_p, Vector3D Tr1_v, Vector3D Tr2_p, Vector3D Tr2_v)
{
	Vector3D dv = Tr1_v - Tr2_v;

	double dv2 = Vector3D.Dot(dv, dv);
	if (dv2 < 0.00000001)      // the  tracks are almost parallel
		return 0.0;             // any time is ok.  Use time 0.

	Vector3D w0 = Tr1_p - Tr2_p;
	double cpatime = -Vector3D.Dot(w0, dv) / dv2;

	return cpatime;             // time of CPA
}

public class SpriteHUDLCD
{
	static Dictionary<string, Color> ColorList = new Dictionary<string, Color> { { "aliceblue", Color.AliceBlue }, { "antiquewhite", Color.AntiqueWhite }, { "aqua", Color.Aqua }, { "aquamarine", Color.Aquamarine }, { "azure", Color.Azure }, { "beige", Color.Beige }, { "bisque", Color.Bisque }, { "black", Color.Black }, { "blanchedalmond", Color.BlanchedAlmond }, { "blue", Color.Blue }, { "blueviolet", Color.BlueViolet }, { "brown", Color.Brown }, { "burlywood", Color.BurlyWood }, { "badetblue", Color.CadetBlue }, { "chartreuse", Color.Chartreuse }, { "chocolate", Color.Chocolate }, { "coral", Color.Coral }, { "cornflowerblue", Color.CornflowerBlue }, { "cornsilk", Color.Cornsilk }, { "crimson", Color.Crimson }, { "cyan", Color.Cyan }, { "darkblue", Color.DarkBlue }, { "darkcyan", Color.DarkCyan }, { "darkgoldenrod", Color.DarkGoldenrod }, { "darkgray", Color.DarkGray }, { "darkgreen", Color.DarkGreen }, { "darkkhaki", Color.DarkKhaki }, { "darkmagenta", Color.DarkMagenta }, { "darkoliveGreen", Color.DarkOliveGreen }, { "darkorange", Color.DarkOrange }, { "darkorchid", Color.DarkOrchid }, { "darkred", Color.DarkRed }, { "darksalmon", Color.DarkSalmon }, { "darkseagreen", Color.DarkSeaGreen }, { "darkslateblue", Color.DarkSlateBlue }, { "darkslategray", Color.DarkSlateGray }, { "darkturquoise", Color.DarkTurquoise }, { "darkviolet", Color.DarkViolet }, { "deeppink", Color.DeepPink }, { "deepskyblue", Color.DeepSkyBlue }, { "dimgray", Color.DimGray }, { "dodgerblue", Color.DodgerBlue }, { "firebrick", Color.Firebrick }, { "floralwhite", Color.FloralWhite }, { "forestgreen", Color.ForestGreen }, { "fuchsia", Color.Fuchsia }, { "gainsboro", Color.Gainsboro }, { "ghostwhite", Color.GhostWhite }, { "gold", Color.Gold }, { "goldenrod", Color.Goldenrod }, { "gray", Color.Gray }, { "green", Color.Green }, { "greenyellow", Color.GreenYellow }, { "doneydew", Color.Honeydew }, { "hotpink", Color.HotPink }, { "indianred", Color.IndianRed }, { "indigo", Color.Indigo }, { "ivory", Color.Ivory }, { "khaki", Color.Khaki }, { "lavender", Color.Lavender }, { "lavenderblush", Color.LavenderBlush }, { "lawngreen", Color.LawnGreen }, { "lemonchiffon", Color.LemonChiffon }, { "lightblue", Color.LightBlue }, { "lightcoral", Color.LightCoral }, { "lightcyan", Color.LightCyan }, { "lightgoldenrodyellow", Color.LightGoldenrodYellow }, { "lightgray", Color.LightGray }, { "lightgreen", Color.LightGreen }, { "lightpink", Color.LightPink }, { "lightsalmon", Color.LightSalmon }, { "lightseagreen", Color.LightSeaGreen }, { "lightskyblue", Color.LightSkyBlue }, { "lightslategray", Color.LightSlateGray }, { "lightsteelblue", Color.LightSteelBlue }, { "lightyellow", Color.LightYellow }, { "lime", Color.Lime }, { "limegreen", Color.LimeGreen }, { "linen", Color.Linen }, { "magenta", Color.Magenta }, { "maroon", Color.Maroon }, { "mediumaquamarine", Color.MediumAquamarine }, { "mediumblue", Color.MediumBlue }, { "mediumorchid", Color.MediumOrchid }, { "mediumpurple", Color.MediumPurple }, { "mediumseagreen", Color.MediumSeaGreen }, { "mediumslateblue", Color.MediumSlateBlue }, { "mediumspringgreen", Color.MediumSpringGreen }, { "mediumturquoise", Color.MediumTurquoise }, { "mediumvioletred", Color.MediumVioletRed }, { "midnightblue", Color.MidnightBlue }, { "mintcream", Color.MintCream }, { "mistyrose", Color.MistyRose }, { "moccasin", Color.Moccasin }, { "navajowhite", Color.NavajoWhite }, { "navy", Color.Navy }, { "oldlace", Color.OldLace }, { "olive", Color.Olive }, { "olivedrab", Color.OliveDrab }, { "orange", Color.Orange }, { "orangered", Color.OrangeRed }, { "orchid", Color.Orchid }, { "palegoldenrod", Color.PaleGoldenrod }, { "palegreen", Color.PaleGreen }, { "paleturquoise", Color.PaleTurquoise }, { "palevioletred", Color.PaleVioletRed }, { "papayawhip", Color.PapayaWhip }, { "peachpuff", Color.PeachPuff }, { "peru", Color.Peru }, { "pink", Color.Pink }, { "plum", Color.Plum }, { "powderblue", Color.PowderBlue }, { "purple", Color.Purple }, { "red", Color.Red }, { "rosybrown", Color.RosyBrown }, { "royalblue", Color.RoyalBlue }, { "saddlebrown", Color.SaddleBrown }, { "salmon", Color.Salmon }, { "sandybrown", Color.SandyBrown }, { "seagreen", Color.SeaGreen }, { "seashell", Color.SeaShell }, { "sienna", Color.Sienna }, { "silver", Color.Silver }, { "skyblue", Color.SkyBlue }, { "slateblue", Color.SlateBlue }, { "slategray", Color.SlateGray }, { "snow", Color.Snow }, { "springgreen", Color.SpringGreen }, { "steelblue", Color.SteelBlue }, { "tan", Color.Tan }, { "teal", Color.Teal }, { "thistle", Color.Thistle }, { "tomato", Color.Tomato }, { "turquoise", Color.Turquoise }, { "violet", Color.Violet }, { "wheat", Color.Wheat }, { "white", Color.White }, { "whitesmoke", Color.WhiteSmoke }, { "yellow", Color.Yellow }, { "yellowgreen", Color.YellowGreen } };
	public IMyTextSurface s = null;

	public SpriteHUDLCD(IMyTextSurface s)
	{
		this.s = s;
	}
	int ltick = -1;
	string lasttext = "-1";
	public void setLCD(string text)
	{
		if (text != lasttext || tick - ltick > 120)
		{
			ltick = tick;
			lasttext = text;
			s.WriteText(text);
			List<object> tok = new List<object>();
			string[] tokens = text.Split(new string[] { "<color=" }, StringSplitOptions.None);
			for (int i = 0; i < tokens.Length; i++)
			{
				var t = tokens[i];
				foreach (var kvp in ColorList)
				{
					if (t.StartsWith(kvp.Key + ">"))
					{
						t = t.Substring(kvp.Key.Length + 1);
						tok.Add(kvp.Value);
						break;
					}
				}
				tok.Add(t);
			}

			s.ContentType = ContentType.SCRIPT;
			s.Script = "";
			s.Font = "Monospace";
			RectangleF _viewport;
			_viewport = new RectangleF(
	(s.TextureSize - s.SurfaceSize) / 2f,
	s.SurfaceSize
);
			using (var frame = s.DrawFrame())
			{
				var zpos = new Vector2(0, 0) + _viewport.Position + new Vector2(s.TextPadding / 100 * s.SurfaceSize.X, s.TextPadding / 100 * s.SurfaceSize.Y);
				var position = zpos;
				Color cColor = Color.White;
				foreach (var t in tok)
				{
					if (t is Color) cColor = (Color)t;
					else if (t is string) writeText((string)t, frame, ref position, zpos, s.FontSize, cColor);
				}
			}
		}
	}

	public void writeText(string text, MySpriteDrawFrame frame, ref Vector2 pos, Vector2 zpos, float textSize, Color color)
	{
		string[] lines = text.Split('\n');
		for (int l = 0; l < lines.Length; l++)
		{
			var line = lines[l];
			if (line.Length > 0)
			{
				MySprite sprite = MySprite.CreateText(line, "Monospace", color, textSize, TextAlignment.LEFT);
				sprite.Position = pos;
				frame.Add(sprite);
			}
			if (l < lines.Length - 1)
			{
				pos.X = zpos.X;
				pos.Y += 28 * textSize;
			}
			else pos.X += 20 * textSize * line.Length;
		}
	}
}


bool matchSpeed = false;

DetectedEntity matchTarget = null;


void toggleMatchSpeed()
{
	matchSpeed = !matchSpeed;
	if (!matchSpeed)
	{
		matchTarget = null;
		foreach (var t in thrusters) t.ThrustOverridePercentage = 0;
	}
}

void speedMatch(string arg)
{
	toggleMatchSpeed();
	if (matchSpeed)
	{
		if (arg == "matchclosest")
		{
			if (detectedEntitiesL.Count > 0)
			{
				matchTarget = detectedEntitiesL[0];
			}
			else toggleMatchSpeed();
		}
		if (arg == "matchfocus")
		{
			MyDetectedEntityInfo? focus = APIWC.GetAiFocus(Me.CubeGrid.EntityId);
			if (focus.HasValue && !focus.Value.IsEmpty())
			{

				matchTarget = null;
				detectedEntitiesD.TryGetValue(focus.Value.EntityId, out matchTarget);
				if (matchTarget == null) toggleMatchSpeed();
			}
			else toggleMatchSpeed();
		}
	}
	else matchTarget = null;
	renderMatch();
}

static Profiler spdmtchP = new Profiler("spdmtch");

bool lastDamp = true;

void speedmatch_upd()
{
	spdmtchP.s();
	if (matchSpeed)
	{
		var over = getCtrl().DampenersOverride;
		if (lastDamp != over)
		{
			lastDamp = over;
			if (!over)
			{
				foreach (var t in thrusters) t.ThrustOverridePercentage = 0;
			}
		}


		bool canTrack = false;
		if (matchTarget != null)
		{
			DetectedEntity d = null;
			detectedEntitiesD.TryGetValue(matchTarget.EntityId, out d);
			if (d != null)
			{
				canTrack = true;
				matchTarget = d;
			}

			if (!canTrack || matchTarget == null)
			{
				toggleMatchSpeed();
			}
			else
			{
				targetVelocityVec = matchTarget.Velocity;
			}
		}
	}
	if (matchSpeed && lastDamp) SpeedMatcher();
	spdmtchP.e();
}




//taken mostly wholesale from https://github.com/Whiplash141/SpaceEngineersScripts/blob/master/Released/speed_matcher.cs
Vector3D targetVelocityVec = new Vector3D(0, 0, 0);


Vector3D ldesire;

void SpeedMatcher()
{
	var ctrl = getCtrl();
	var myVelocityVec = ctrl.GetShipVelocities().LinearVelocity;
	var inputVec = ctrl.MoveIndicator;
	var desiredDirectionVec = Vector3D.TransformNormal(inputVec, ctrl.WorldMatrix); //world relative input vector 
	var relativeVelocity = myVelocityVec - targetVelocityVec;
	var rvsqr = relativeVelocity.LengthSquared();
	if (tick % 2 == 0 || ldesire != desiredDirectionVec || (rvsqr > 0.01 * 0.01 && rvsqr < 50 * 50))
	{//speedmatch is surprisingly unperformant even with all we can do. use heuristic to do half-ticks a lot of the time
		ldesire = desiredDirectionVec;
		ApplyThrust(thrusters, relativeVelocity, desiredDirectionVec, ctrl);
	}
}


void ApplyThrust(List<IMyThrust> thrusters, Vector3D targetVel, Vector3D dirvec, IMyShipController ctrl)
{
	var mass = ctrl.CalculateShipMass().PhysicalMass;
	var gravity = ctrl.GetNaturalGravity();

	var desiredThrust = mass * (2 * targetVel + gravity);
	var thrustToApply = desiredThrust;
	if (!Vector3D.IsZero(dirvec))
	{
		thrustToApply = VectorRejection(desiredThrust, dirvec);
	}
	applySpeedMatchThrust(thrustToApply, dirvec, ctrl.DampenersOverride);
}

public Vector3D VectorRejection(Vector3D a, Vector3D b) //reject a on b    
{
	if (Vector3D.IsZero(b))
		return Vector3D.Zero;

	return a - a.Dot(b) / b.LengthSquared() * b;
}


static Profiler aavtP = new Profiler("aavt");

static public void applySpeedMatchThrust(Vector3D thrustNewtons, Vector3D desiredDirectionVec, bool dampener)
{
	aavtP.s();
	//curThrustVector = thrustNewtons;

	foreach (var k in Base6Directions.EnumDirections)
	{
		var l = thrustersByDir[k];
		if (l.Count > 0)
		{
			var fwd = l[0].WorldMatrix.Forward;
			var bck = l[0].WorldMatrix.Backward;
			var fwddot = Vector3D.Dot(fwd, thrustNewtons);
			var bckdot = Vector3D.Dot(bck, desiredDirectionVec);
			foreach (IMyThrust t in l)
			{
				if (bckdot > .7071) //thrusting in desired direction
				{
					t.ThrustOverridePercentage = 1f;
				}
				else if (fwddot > 0.00001f && dampener)
				{
					var met = thrusterMaxEffectiveThrust[t];
					var outputProportion = MathHelper.Clamp(fwddot / met, 0, 1);
					var achievedot = outputProportion * met;
					fwddot -= achievedot;
					t.ThrustOverridePercentage = (float)outputProportion;
				}
				else t.ThrustOverridePercentage = 0.000001f;
			}
		}
	}
	aavtP.e();
}

public static List<Setting_> settings = null;

public static string serializeConfig()
{
	string s = "";
	foreach (var v in settings)
	{
		if (s.Length > 0) s += "\n";
		if (v.desc.Length > 0)
		{
			s += "//" + v.desc + "\n";
		}
		s += v.serialized();
	}
	return s;
}

public static void deserializeConfig(string s)
{
	if (settings == null) throw new Exception("setting list null?");
	string[] lines = s.Split('\n');
	foreach (var l in lines)
	{
		if (l.StartsWith("//") || l.StartsWith("#")) continue;//a=b
		int idx = l.IndexOf('=');
		if (idx != -1)
		{
			var a = l.Substring(0, idx);
			var b = l.Substring(idx + 1);
			Setting_ set = null;
			foreach (var stn in settings)
			{
				if (stn != null && a == stn.name)
				{
					set = stn;
					break;
				}
			}
			if (set != null)
			{
				set.deserializeValue(b);
			}
		}
	}
}

public static void resetConfig()
{
	foreach (var v in settings) v.reset();
}



public abstract class Setting_
{
	public string name = "";
	public string desc = "";
	public Setting_(string n)
	{
		name = n;
		if (settings == null) settings = new List<Setting_>();
		settings.Add(this);
	}
	protected string setvalue = "";
	abstract public void serializeValue();
	public string serialized()
	{
		serializeValue();
		return name + "=" + setvalue;
	}
	public abstract void deserializeValue(string v);

	public T typeString<T>(string v)
	{
		try
		{
			object V_ = null;
			if (typeof(T) == typeof(string)) V_ = v;
			else if (typeof(T) == typeof(int)) V_ = Int32.Parse(v);
			else if (typeof(T) == typeof(float)) V_ = Single.Parse(v);
			else if (typeof(T) == typeof(double)) V_ = Double.Parse(v);
			else if (typeof(T) == typeof(char)) V_ = Char.Parse(v);
			else if (typeof(T) == typeof(DateTime)) V_ = DateTime.Parse(v);
			else if (typeof(T) == typeof(decimal)) V_ = Decimal.Parse(v);
			else if (typeof(T) == typeof(bool)) V_ = Boolean.Parse(v);
			else if (typeof(T) == typeof(byte)) V_ = Byte.Parse(v);
			else if (typeof(T) == typeof(uint)) V_ = UInt32.Parse(v);
			else if (typeof(T) == typeof(short)) V_ = short.Parse(v);
			else if (typeof(T) == typeof(long)) V_ = long.Parse(v);
			else if (typeof(T) == typeof(List<string>))
			{
				V_ = new List<string>(v.Split(','));
			}
			else
			{
				log("Unsupported type in typeString for " + typeof(T).ToString(), LT.LOG_N);
				return default(T);
			}
			return (T)V_;
		}
		catch (Exception e)
		{
			log("Exception in typeString: " + e.Message, LT.LOG_N);
			return default(T);
		}
	}
	public abstract void reset();
}

public class SettingDict<T> : Setting_
{
	public Dictionary<string, T> DVal = new Dictionary<string, T>();
	public Dictionary<string, T> Val = new Dictionary<string, T>();
	public SettingDict(string n, Dictionary<string, T> def = null) : base(n)
	{
		if (def != null) Val = def;
	}

	public override void deserializeValue(string v)
	{
		Val.Clear();
		string[] kvps = v.Split(',');
		foreach (var p in kvps)
		{
			string[] kvp = p.Split(':');
			if (kvp.Length > 1)
			{
				Val[kvp[0]] = typeString<T>(kvp[1]);
			}
		}
	}

	public override void serializeValue()
	{
		string av = "";
		foreach (var kvp in Val)
		{
			if (av.Length > 0) av += ",";
			av += kvp.Key + ":" + kvp.Value.ToString();
		}
		setvalue = av;
	}

	public SettingDict<T> Desc(string d)
	{
		desc = d;
		return this;
	}
	public SettingDict<T> Default(Dictionary<string, T> v)
	{
		DVal = Val = v;
		return this;
	}
	public override void reset()
	{
		Val = DVal;
	}
}


public class Setting<T> : Setting_
{
	public T Val;
	T DVal;

	T defaultV()
	{
		if (typeof(T) == typeof(String)) return (T)(object)String.Empty;
		return default(T);
	}
	public Setting(string n) : base(n)
	{
		typeString<T>(defaultV().ToString());//will trip the invalid type error if it is invalid
		Val = defaultV();
	}

	public Setting<T> Desc(string d)
	{
		desc = d;
		return this;
	}
	public Setting<T> Default(T v)
	{
		DVal = Val = v;
		return this;
	}

	override public void serializeValue()
	{
		try
		{
			if (typeof(T) == typeof(List<string>))
			{
				object V_ = Val;
				List<string> Vl = (List<string>)V_;
				setvalue = String.Join(",", Vl.ToArray());
			}
			else setvalue = "" + Val;
		}
		catch (Exception e)
		{
			log("Exception in serializeValue: " + e.Message, LT.LOG_N);
		}
	}
	override public void deserializeValue(string v)
	{
		Val = typeString<T>(v);
		//return V;
	}
	public override void reset()
	{
		Val = DVal;
	}
}

static IMyTextSurface consoleLog = null;

static IMyTextSurface statusLog = null;

static IMyTextSurface profileLog = null;

static IMyTextSurface PDCLog = null;

static List<IMyTerminalBlock> weaponCoreWeapons = new List<IMyTerminalBlock>();

static List<IMyTerminalBlock> subsystemTargeters = new List<IMyTerminalBlock>();

static List<IMyFunctionalBlock> desyncGroup = new List<IMyFunctionalBlock>();

static List<IMyFunctionalBlock> PDCGroup = new List<IMyFunctionalBlock>();

static List<IMyPowerProducer> powergens = new List<IMyPowerProducer>();

static List<IMyShipController> controllers = new List<IMyShipController>();

public static List<IMyGyro> gyros = new List<IMyGyro>();

static List<IMyCargoContainer> all_cargos = new List<IMyCargoContainer>();

static List<IMyCargoContainer> loot_cargos = new List<IMyCargoContainer>();

static List<IMyShipConnector> connectors = new List<IMyShipConnector>();

static List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();

static AggregateInventoryInterface ammoInterface = new AggregateInventoryInterface();

static AggregateInventoryInterface cargoInterface = new AggregateInventoryInterface();

static AggregateInventoryInterface lootInterface = new AggregateInventoryInterface();

static AggregateInventoryInterface connectorInterface = new AggregateInventoryInterface();

static AggregateInventoryInterface grinderInterface = new AggregateInventoryInterface();

static List<IMyGasGenerator> extractors = new List<IMyGasGenerator>();

static List<IMyGasTank> tanks_hydrogen = new List<IMyGasTank>();

static public List<IMyThrust> _thrusters = new List<IMyThrust>();

static public WcPbApi APIWC = null;

static public ResourceLoader resourceLoader = null;

public class ResourceLoader
{
	public Program p;

	public bool neverFullyLoaded = true;
	public ResourceLoader()
	{
		mkBlockCheckMachine();
	}

	bool readConfig = false;

	public void update()
	{
		if (APIWC == null)
		{
			APIWC = new WcPbApi();
			try
			{
				APIWC.Activate(gProgram.Me);
			}
			catch (Exception) { }

		}
		if (!APIWC.isReady && tick % 30 == 0)
		{
			try
			{
				APIWC.Activate(gProgram.Me);
			}
			catch (Exception) { }
		}
		if (!APIWC.isReady) return;

		if (!readConfig || tick % 60 == 0)
		{
			readConfig = true;
			if (p.Me.CustomData != lastCustomData)
			{
				log("Loading CustomData.", LT.LOG_N);
				deserializeConfig(p.Me.CustomData);
				p.Me.CustomData = lastCustomData = serializeConfig();
			}
		}

		if (blockCheckMachine != null)
		{
			if (!blockCheckMachine.MoveNext())
			{
				blockCheckMachine.Dispose();
				blockCheckMachine = null;
			}
		}
		else if (readConfig && tick % (5 * 60 * 60) == 0) mkBlockCheckMachine();
	}
	public string lastCustomData = "-1";

	IEnumerator<bool> blockCheckMachine = null;
	void mkBlockCheckMachine()
	{
		if (blockCheckMachine != null) blockCheckMachine.Dispose();
		blockCheckMachine = blockLoader();
		step = 0;
	}
	public int step = 0;

	public bool isThis(IMyTerminalBlock b)
	{
		return b.OwnerId == p.Me.OwnerId && b.CubeGrid == p.Me.CubeGrid;
	}
	public IEnumerator<bool> blockLoader()
	{
		var gts = p.GridTerminalSystem;
		consoleLog = null;
		statusLog = null;
		profileLog = null;
		List<IMyTerminalBlock> LCDs = new List<IMyTerminalBlock>();
		gts.GetBlocksOfType(LCDs, b => (b is IMyTextSurface) && b.CubeGrid == p.Me.CubeGrid);
		foreach (var b in LCDs)
		{
			IMyTextSurface s = b as IMyTextSurface;
			if (b.CustomData.Contains("radarLog")) statusLog = s;
			else if (b.CustomData.Contains("consoleLog")) consoleLog = s;
			else if (b.CustomData.Contains("profileLog")) profileLog = s;
			else if (b.CustomData.Contains("PDCLog")) PDCLog = s;
		}
		step++;
		yield return true;
		gts.GetBlocksOfType(controllers, isThis);
		step++;
		yield return true;
		gyros.Clear();
		gts.GetBlocksOfType(gyros, isThis);
		step++;
		yield return true;
		all_cargos.Clear();
		gts.GetBlocksOfType(all_cargos, isThis);
		cargoInterface.setContainers(all_cargos);
		yield return true;
		step++;
		connectors.Clear();
		gts.GetBlocksOfType(connectors, isThis);
		connectorInterface.setContainers(connectors);
		yield return true;
		step++;
		_thrusters.Clear();
		gts.GetBlocksOfType(_thrusters, b => b.CubeGrid == p.Me.CubeGrid);
		if (thrusters.Count == 0) thrusters.AddRange(_thrusters);
		yield return true;
		step++;
		updateMET();
		yield return true;
		step++;
		updateThrustByDir();
		yield return true;
		step++;
		gts.GetBlocksOfType(powergens, b => b.CubeGrid == p.Me.CubeGrid);
		yield return true;
		step++;
		gts.GetBlocksOfType(weaponCoreWeapons, b => b.CubeGrid == p.Me.CubeGrid && b.IsFunctional && APIWC.HasCoreWeapon(b));

		loot_cargos.Clear();
		foreach (var b in all_cargos) if (b.CustomName.Contains("Loot")) loot_cargos.Add(b);
		lootInterface.setContainers(loot_cargos);
		yield return true;
		step++;
		List<IMyTerminalBlock> tmp = new List<IMyTerminalBlock>();
		tmp.AddArray(all_cargos.ToArray());
		tmp.AddArray(connectors.ToArray());
		ammoInterface.setContainers(tmp);
		yield return true;
		step++;
		p.GridTerminalSystem.GetBlocksOfType(grinders, isThis);
		grinderInterface.setContainers(grinders);
		yield return true;
		step++;
		IMyBlockGroup ind = gts.GetBlockGroupWithName(Config.RSubtarget.Val);
		if (ind != null)
		{
			ind.GetBlocksOfType(subsystemTargeters);
		}
		yield return true;
		step++;

		ind = gts.GetBlockGroupWithName(Config.WeaponDesync.Val);
		if (ind != null)
		{
			ind.GetBlocksOfType(desyncGroup);
		}
		yield return true;
		step++;

		ind = gts.GetBlockGroupWithName(Config.PDCGroup.Val);
		if (ind != null)
		{
			ind.GetBlocksOfType(PDCGroup);
			List<IMyFunctionalBlock> k = new List<IMyFunctionalBlock>();
			foreach (var b in PDCGroup) if (weaponCoreWeapons.Contains(b)) k.Add(b);
			PDCGroup = k;
		}
		yield return true;
		step++;
		if (PDCBlockGroupK.Count == 0)
		{
			List<IMyBlockGroup> bg = new List<IMyBlockGroup>();
			gts.GetBlockGroups(bg, g => g.Name.StartsWith("PDCGroup"));
			yield return true;
			step++;
			foreach (var bb in bg)
			{
				List<IMyFunctionalBlock> blox = new List<IMyFunctionalBlock>();
				bb.GetBlocksOfType(blox);
				List<IMyFunctionalBlock> k = new List<IMyFunctionalBlock>();
				foreach (var b in blox)
				{
					if (weaponCoreWeapons.Contains(b))
					{
						if (PDCDataSubType.ContainsKey(b.DefinitionDisplayNameText)) k.Add(b);
						else log("unknown pdc type \"" + b.DefinitionDisplayNameText + "\"");
					}
				}
				PDCBlockGroups[bb.Name] = k;
				PDCBlockGroupK.Add(bb.Name);
				yield return true;
				step++;
			}
			if (PDCBlockGroupK.Count == 0 && PDCGroup.Count != 0)
			{
				PDCBlockGroups[Config.PDCGroup.Val] = PDCGroup;
				PDCBlockGroupK.Add(Config.PDCGroup.Val);
			}
			updPDCNetGroups();
			yield return true;
			step++;
		}
		gts.GetBlocksOfType(extractors, b => b.OwnerId == p.Me.OwnerId && b.CubeGrid == p.Me.CubeGrid && (b.DefinitionDisplayNameText == "Extractor" || b.DefinitionDisplayNameText == "Small Fuel Extractor"));
		yield return true;
		step++;
		gts.GetBlocksOfType(tanks_hydrogen, b => b.OwnerId == p.Me.OwnerId && b.CubeGrid == p.Me.CubeGrid && b.DefinitionDisplayNameText.Contains("Hydrogen"));
		yield return true;
		step++;
		if (neverFullyLoaded) log("BOOT DONE. " + tick + "t (" + (((float)tick) / 60).ToString("0.0") + "s)", LT.LOG_N);
		neverFullyLoaded = false;
		step++;
		yield return false;
	}
}

static Dictionary<string, List<IMyFunctionalBlock>> PDCBlockGroups = new Dictionary<string, List<IMyFunctionalBlock>>();

static List<string> PDCBlockGroupK = new List<string>();

List<MyDetectedEntityInfo> WCobstructions = new List<MyDetectedEntityInfo>();

Dictionary<MyDetectedEntityInfo, float> WCthreats = new Dictionary<MyDetectedEntityInfo, float>();

MyDetectedEntityInfo focus = new MyDetectedEntityInfo();

long lfocus = -1;

int focusChangeTick = -1;

class DetectedEntity
{
	public int updTick;

	public long EntityId;
	public string Name = "";
	public MyDetectedEntityType Type;
	public BoundingBoxD BBox;
	public MatrixD Orientation;
	public Vector3D Position;
	public Vector3D Velocity;
	public MyRelationsBetweenPlayerAndBlock Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
	public float threat;

	public MyDetectedEntityInfo focus;
	public double ldistSqr;
	public double distSqr;

	public bool isPMW = false;


	public DetectedEntity upd(MyDetectedEntityInfo e)
	{
		if (e.IsEmpty()) return this;
		updTick = tick;
		EntityId = e.EntityId;
		if (e.Name.Length > 0) Name = e.Name;
		Type = e.Type;
		Orientation = e.Orientation;
		Position = e.Position;
		Velocity = e.Velocity;
		BBox = e.BoundingBox;
		Rel = e.Relationship;
		if ((e.Type == MyDetectedEntityType.CharacterHuman || e.Type == MyDetectedEntityType.CharacterOther) && Name.Length == 0)
		{
			Name = "Suit";// + e.EntityId;
		}
		if (e.Type == MyDetectedEntityType.Unknown)
		{//unknown means obstruction list generally
			if (e.Name.StartsWith("MyVoxelMap"))
			{
				Type = MyDetectedEntityType.Asteroid;
				Name = "Asteroid";
				Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
			}
			else if (e.Name.Length == 0)
			{
				var he = BBox.Max - BBox.Min;
				//grids this small don't actually show up in obstruction list, only suits.
				if (he.X < 3 && he.Y < 3 && he.Z < 3)
				{
					Type = MyDetectedEntityType.CharacterHuman;
					Rel = MyRelationsBetweenPlayerAndBlock.Friends;
					Name = "Suit";
				}
			}
			else Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
		}
		else if (e.Type == MyDetectedEntityType.Asteroid) Name = "Asteroid";
		else if (e.Type == MyDetectedEntityType.Planet) Name = "Planet";
		if (e.Type == MyDetectedEntityType.LargeGrid)
		{
			try
			{
				focus = APIWC.GetAiFocus(EntityId).GetValueOrDefault();
			}
			catch (Exception) { }
		}
		return this;
	}
	const double divisor = 1.0d / 60.0d;
	public DetectedEntity upd(MyDetectedEntityInfo e, float t)
	{
		upd(e);
		threat = t;
		if (e.Name.StartsWith("Small Grid") && Type == MyDetectedEntityType.SmallGrid) isPMW = true;
		if (Type == MyDetectedEntityType.SmallGrid && !isPMW)
		{
			var he = BBox.Max - BBox.Min;
			if (he.X < 10 && he.Y < 10 && he.Z < 10) isPMW = true;
		}
		return this;
	}
	public Vector3D getEstPos()
	{
		if (updTick == tick) return Position;
		return Position + (Velocity * (tick - updTick) * divisor);
	}
}

Dictionary<long, DetectedEntity> detectedEntitiesD = new Dictionary<long, DetectedEntity>();

List<DetectedEntity> detectedEntitiesL = new List<DetectedEntity>();

void addDE(DetectedEntity e)
{
	detectedEntitiesD[e.EntityId] = e;
	detectedEntitiesL.Add(e);
}

void remDE(DetectedEntity e)
{
	detectedEntitiesD.Remove(e.EntityId);
	detectedEntitiesL.Remove(e);
}

int stale_threshold = 20;


int lastUpdTick = 0;


public enum RDRSCT
{
	OBS1, THRT1, OBS2, THRT2, THRT3, SPD, STRG, MVRS, DTCT, SPRITELCD, ALL
}


SpriteHUDLCD statusLogSprite = null;

static Profiler radarP = new Profiler("radar");

public void updRadar(RDRSCT section = RDRSCT.ALL)
{
	radarP.s();
	if (tick - lastUpdTick > 60 || section != RDRSCT.ALL)
	{
		try
		{
			lastUpdTick = tick;
			processThreats(section);
			if (statusLog != null)
			{
				var rndr = renderThreats(section);
				if (section >= RDRSCT.SPRITELCD)
				{
					if (statusLogSprite == null) statusLogSprite = new SpriteHUDLCD(statusLog);
					statusLogSprite.s = statusLog;
					statusLogSprite.setLCD(rndr);
				}
			}
		}
		catch (Exception) { }
	}
	else
	{
		if (tick % 10 == 0 && section == RDRSCT.ALL)
		{
			renderWeapons();
		}
	}
	radarP.e();
}

public void processThreats(RDRSCT section)
{
	if (!APIWC.isReady) return;
	my_id = Me.CubeGrid.EntityId;
	my_pos = Me.GetPosition();
	focus = APIWC.GetAiFocus(my_id, 0).GetValueOrDefault();

	if (section >= RDRSCT.OBS1)
	{

		if (focus.EntityId != lfocus)
		{
			lfocus = focus.EntityId;
			focusChangeTick = tick;
		}
		WCobstructions.Clear();
		APIWC.GetObstructions(Me, WCobstructions);
	}
	if (section >= RDRSCT.THRT1)
	{
		WCthreats.Clear();
		APIWC.GetSortedThreats(WCthreats);
	}
	if (section >= RDRSCT.OBS2)
	{
		foreach (var o in WCobstructions)
		{
			if (!o.IsEmpty())
			{
				DetectedEntity de = null;
				detectedEntitiesD.TryGetValue(o.EntityId, out de);
				if (de != null) de.upd(o);
				else
				{
					if ((o.Type == MyDetectedEntityType.Asteroid) || (o.Type == MyDetectedEntityType.Unknown))
					{
						if (!o.Name.StartsWith("MyVoxelMap")) {
							addDE(new DetectedEntity().upd(o));
						}
					}					
				}
			}
		}
	}
	if (section >= RDRSCT.THRT2)
	{
		foreach (var kvp in WCthreats)
		{
			if (!kvp.Key.IsEmpty())
			{
				DetectedEntity de = null;
				detectedEntitiesD.TryGetValue(kvp.Key.EntityId, out de);
				if (de != null) de.upd(kvp.Key).threat = kvp.Value;
				else
				{
					var n = new DetectedEntity();
					n.upd(kvp.Key).threat = kvp.Value;
					addDE(n);
				}
			}
		}
	}
	if (section == 0 || section >= RDRSCT.THRT3)
	{
		List<DetectedEntity> del = new List<DetectedEntity>();
		foreach (var e in detectedEntitiesL)
		{
			if (tick - e.updTick > stale_threshold) del.Add(e);
			else
			{
				e.ldistSqr = e.distSqr;
				e.distSqr = (my_pos - e.Position).LengthSquared();
			}
		}
		foreach (var e in del) remDE(e);
	}
}


string getColFromRel(MyRelationsBetweenPlayerAndBlock rel)
{
	if (rel == MyRelationsBetweenPlayerAndBlock.Enemies) return Config.REC.Val;
	else if (rel == MyRelationsBetweenPlayerAndBlock.Owner) return "blue";//never happens?
	else if (rel == MyRelationsBetweenPlayerAndBlock.Friends || rel == MyRelationsBetweenPlayerAndBlock.FactionShare) return Config.RFC.Val;
	else if (rel == MyRelationsBetweenPlayerAndBlock.Neutral) return Config.RNC.Val;
	else return Config.ROC.Val;
}

public static string dist2str(double d)
{
	if (d > 1000)
	{
		return (d / 1000).ToString("0.0") + "km";
	}
	else return d.ToString("0") + "m";
}


static void bapp(StringBuilder b, params object[] args)
{
	foreach (object a in args)
	{
		b.Append(a.ToString());
	}
}


Dictionary<int, string> radarBlocks = new Dictionary<int, string>();


long my_id = 0;

Vector3D my_pos;


bool renderDirty = false;

public void renderSort()
{
	detectedEntitiesL.Sort(delegate (DetectedEntity x, DetectedEntity y) {
		double dx = x.distSqr;
		if (x.isPMW) dx += 1000000000;
		double dy = y.distSqr;
		if (y.isPMW) dy += 1000000000;
		return dx.CompareTo(dy);
	});

}

string renderedMatch = "";

public void renderMatch()
{
	if (my_id == 0) my_id = Me.CubeGrid.EntityId;
	StringBuilder b = new StringBuilder();
	var trg = APIWC.GetAiFocus(my_id).GetValueOrDefault();
	//if (section >= RDRSCT.SPD)
	{
		if (matchSpeed)
		{
			b.Append("<color=white>!<color=green>SPEEDMATCHING");
			if (trg.IsEmpty() || trg.Name != matchTarget.Name)
			{
				bapp(b, ":<color=", getColFromRel(matchTarget.Rel), ">", matchTarget.Name);
			}
			else b.Append(" ON");
			b.Append("\n");
		}
		else b.Append("\n");

		if (autoRotate)
		{
			b.Append("<color=white>!<color=lightblue>AUTOROTATING");
			if (trg.IsEmpty() || trg.Name != rotateTarget.Name)
			{
				bapp(b, ":<color=", getColFromRel(rotateTarget.Rel), ">", rotateTarget.Name);
			}
			else b.Append(" ON");
			b.Append("\n");
		}
		else b.Append("\n");

		if (matchSpeed || !trg.IsEmpty())
		{
			Vector3D tp = Vector3D.Zero;
			Vector3D tv = Vector3D.Zero;
			if (!matchSpeed || (matchSpeed && trg.Name == matchTarget.Name))
			{
				tp = trg.Position;
				tv = trg.Velocity;
			}
			else if (matchSpeed && matchTarget != null)
			{
				tp = matchTarget.getEstPos();
				tv = matchTarget.Velocity;
			}
			if (tp != Vector3D.Zero)
			{
				var cpat = cpa_time(getPosition(), getVelocity(), tp, tv);
				b.Append("<color=lightgray>CPA:");//, rotateTarget.Name);
				if (cpat < 0) b.Append("moving away");
				else
				{
					var mf = getPosition() + (getVelocity() * cpat);
					var tf = tp + (tv * cpat);
					var d = Vector3D.Distance(mf, tf);
					bapp(b, dist2str(d), " in ", cpat.ToString("0.0"), "s");
				}
				b.Append("\n");
			}
		}
		b.Append("\n");
		if (!trg.IsEmpty())
		{
			double d = (trg.Position - my_pos).Length();
			bapp(b, "<color=lightgray>Target: <color=red>", trg.Name, " (", dist2str(d), ")\n");
		}
		else b.Append("<color=lightgray>Target: none\n");

	}

	string r = b.ToString();
	if (r != renderedMatch)
	{
		renderedMatch = r;
		renderDirty = true;
	}
}

string renderedWeapons = "";

public void renderWeapons()
{
	StringBuilder b = new StringBuilder();
	//if (section >= RDRSCT.STRG)
	{
		if (subsystemTargeters.Count > 0)
		{
			foreach (var blk in subsystemTargeters)
			{
				var rdy = APIWC.IsWeaponReadyToFire(blk);
				if (rdy) bapp(b, "     <color=lightgreen>", blk.CustomName);
				else
				{
					var ws = getWS(blk);
					if (ws != null && ws.settings != null)
					{
						var timeleft = (1.0 - ws.chargeProgress) * ws.settings.chargeTicks / 60;
						if (ws.lastDrawFactor != 0) timeleft /= ws.lastDrawFactor;
						var chrgt = timeleft.ToString("0.0");

						if (chrgt.Length < 3) b.Append(" ");
						bapp(b, "<color=orange>", chrgt, "s ", blk.CustomName);
					}
				}
				var t = APIWC.GetWeaponTarget(blk).GetValueOrDefault();
				if (t.Type == MyDetectedEntityType.LargeGrid || t.Type == MyDetectedEntityType.SmallGrid) bapp(b, " ► ", t.Name);
				else b.Append(" ► <color=lightgray>No target");
				b.Append("\n");
			}
		}
	}
	string r = b.ToString();
	if (r != renderedWeapons)
	{
		renderedWeapons = r;
		renderDirty = true;
	}
}

string renderedMovers = "";

public void renderMovers()
{
	StringBuilder b = new StringBuilder();
	//if (section >= RDRSCT.MVRS)
	{
		int PMWs = 0;
		foreach (var e in detectedEntitiesL)
		{
			if (e.isPMW) PMWs++;
		}
		var plo = APIWC.GetProjectilesLockedOn(my_id);
		var plocked = plo.Item2;
		if (plocked > 0)
		{
			bapp(b, "<color=white>!<color=red>INBOUND TORPS:<color=white>", plocked, "\n");
		}
		if (PMWs > 0)
		{
			bapp(b, "<color=white>!<color=red>Probable PMWs:<color=white>", PMWs, "\n");
		}
		b.Append("\n");
	}
	string r = b.ToString();
	if (r != renderedMovers)
	{
		renderedMovers = r;
		renderDirty = true;
	}
}

string renderedDetectedEntities = "";

public void renderDetectedEntities()
{
	StringBuilder b = new StringBuilder();
	//if (section >= RDRSCT.DTCT)
	{
		for (int i = 0; i < detectedEntitiesL.Count; i++)
		{
			var e = detectedEntitiesL[i];

			bapp(b, "<color=", getColFromRel(e.Rel), ">");

			bapp(b, e.Name, " (", dist2str(Math.Sqrt(e.distSqr)), ")");
			string thrt;
			if (e.threat < 0.0001) thrt = "0";
			else if (e.threat > 0.1) thrt = e.threat.ToString("0.0");
			else if (e.threat > 0.01) thrt = e.threat.ToString("0.00");
			else thrt = "<0.01";

			if (e.Rel == MyRelationsBetweenPlayerAndBlock.Enemies) b.Append(" t:" + thrt);

			bapp(b, " v:", dist2str(e.Velocity.Length()), "/s");

			if (!e.focus.IsEmpty())
			{
				b.Append("\n └target:");
				if (e.focus.Relationship == MyRelationsBetweenPlayerAndBlock.Friends) b.Append("<color=lightgreen>");
				else b.Append("<color=lightgray>");
				b.Append(e.focus.Name);
			}
			b.Append("\n");
		}
	}
	string r = b.ToString();
	if (r != renderedDetectedEntities)
	{
		renderedDetectedEntities = r;
		renderDirty = true;
	}
}


public string renderedThreats = "";


public string renderThreats(RDRSCT section)
{
	try
	{
		my_id = Me.CubeGrid.EntityId;
		my_pos = Me.GetPosition();
		renderSort();
		if (section >= RDRSCT.SPD) renderMatch();
		if (section >= RDRSCT.STRG) renderWeapons();
		if (section >= RDRSCT.MVRS) renderMovers();
		if (section >= RDRSCT.DTCT) renderDetectedEntities();

		if (renderDirty)
		{
			StringBuilder b = new StringBuilder();
			bapp(b, renderedMatch, renderedWeapons, renderedMovers, renderedDetectedEntities);
			renderedThreats = b.ToString();
			renderDirty = false;
		}
		return renderedThreats;
	}
	catch (Exception e)
	{
		Echo(e.ToString());
		return e.ToString();
	}
	//return b.ToString();
}

static public Program gProgram = null;

static public DateTime bootTime;

public Program()
{
	gProgram = this;
	resourceLoader = new ResourceLoader();
	resourceLoader.p = this;
	wsinit();
	bootTime = DateTime.Now;

	log("BOOT", LT.LOG_N);
	Config = new Config_();
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Save()
{
	//GridTerminalSystem.GetBlockWithName("Cockpit").CustomData = "Save() called";
	// This method is called whenever the script might cease for any reason, be it pblimiter burnout (depending on pblimiter settings), disabled block, save, power failure, etc.
	//so let's release gyros and thrusters here. If the script is still going, it'll take them back if necessary next tick.
	if (matchSpeed || autoRotate) foreach (var g in gyros) g.GyroOverride = false;
	if (matchSpeed) foreach (var t in thrusters) t.ThrustOverride = 0;
	if (matchSpeed && getCtrl().DampenersOverride)
	{
		getCtrl().DampenersOverride = false;
		save_putdampon = true;
		//we don't want a burnout while grinding some npc to send us rocketing into it
		//this will flick it back on in a moment, if it turns out we're not being destroyed by pblimiter here
	}
}

bool save_putdampon = false;


const double MAX_MS_PER_SEC_BOOT = 0.16;

const double MAX_MS_PER_SEC = 0.25;

const int PBLIMIT_STARTUPTICKS = 0;//20 by default

class BurnoutTrack
{
	public double maxmspersec = 0.25;
	public static double[] defertrack;
	public int len = 60;
	public BurnoutTrack(int l, double ms)
	{
		len = l;
		maxmspersec = ms;
		defertrack = new double[len];
	}
	int defercalls = 0;
	int deferpos = 0;
	static bool hangflag = false;
	int hangticks = 0;
	int hangtick = 0;
	bool fsdbg = false;
	DateTime bf = DateTime.Now;
	public bool burnoutpre()
	{
		bf = DateTime.Now;
		if (hangflag)
		{
			if (tick > hangtick)
			{
				double avg = 0;
				foreach (var d in defertrack) avg += d;
				avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
				if (avg > maxmspersec * len / 60)
				{
					defertrack[deferpos] = 0;
					defercalls += 1;
					deferpos = (deferpos + 1) % defertrack.Length;
					return true;
				}
				else
				{
					hangflag = false;
					//log("Resuming after " + (hangticks / 60.0d).ToString("0.0") + "s", LT.LOG_N);
				}
			}
		}
		return hangflag;
	}
	public double avg()
	{
		double avg = 0;
		foreach (var d in defertrack) avg += d;
		avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
		return avg;
	}
	public bool burnoutpost()
	{
		double ms = (DateTime.Now - bf).TotalMilliseconds;
		defertrack[deferpos] = ms;
		defercalls += 1;
		deferpos = (deferpos + 1) % defertrack.Length;
		if (!hangflag)
		{
			double p_avg = 0;
			foreach (var d in defertrack) p_avg += d;
			int divisor = defercalls > defertrack.Length ? defertrack.Length : defercalls;
			var avg = p_avg / divisor;
			var mtch = maxmspersec * len / 60;
			if (avg > mtch)
			{
				int tickstodoom = PBLIMIT_STARTUPTICKS - tick;
				if (tickstodoom > 0 && tickstodoom * maxmspersec < avg) return false;

				int waitticks = 0;
				while (p_avg / (divisor + waitticks) > mtch) waitticks++;

				hangticks = waitticks;
				hangtick = tick + waitticks;
				hangflag = true;


				var lstr = tick + ": " + avg.ToString("0.00") + ">" + (mtch).ToString("0.00") + "ms/s exec. Sleeping " + (hangticks / 60.0d).ToString("0.0") + "s";
				log(lstr, LT.LOG_N);
				var c = getCtrl();
				if (c != null)
				{
					/*if (!fsdbg)
					{
						c.CustomData = "";
						fsdbg = true;
					}
					c.CustomData += "\n\n" + lstr + "\n\n" + Profiler.getAllReports();*/
				}
				else getCtrlTick = -9000;

				return true;
			}
		}
		else return true;
		return false;
	}
}


public static int tick = -1;

static BurnoutTrack bt60 = new BurnoutTrack(60, MAX_MS_PER_SEC_BOOT);


static Profiler initP = new Profiler("init");

static Profiler mainP = new Profiler("main");

#region premain
public void Main(string arg, UpdateType upd)
{
	tick += 1;
	#region burnoutfailsafepre
	if (bt60.burnoutpre()) return;
	#endregion

	if (tick % 20 == 0) if (Me.Closed)
		{
			Runtime.UpdateFrequency = UpdateFrequency.None;
			return;
		}
	mainP.start();
	main(arg, upd);
	mainP.stop();
	if (tick % 5 == 0)
	{
		Echo(tick.ToString());
		if (profileLog != null) profileLog.WriteText("name:ms1t:ms60t\n" + Profiler.getAllReports());
	}
	if (consoleLog != null && tick % 5 == 0)
	{
		if (Logger.loggedMessagesDirty)
		{
			Logger.updateLoggedMessagesRender();
			consoleLog.WriteText(Logger.loggedMessagesRender);
		}
	}
	#region burnoutfailsafepost
	if (bt60.burnoutpost()) return;
	#endregion
}

#endregion

int startitr = 0;

int postboottick = int.MaxValue;


GyroECU5 gyroECU = new GyroECU5();

bool first = true;

void main(string arg, UpdateType upd)
{
	initP.start();
	if (tick % 10 == 0)
	{
		resourceLoader.update();
	}
	initP.stop();
	if (resourceLoader.neverFullyLoaded)
	{
		Echo("INITIALIZING: " + resourceLoader.step + "/22");
		if (statusLog != null) statusLog.WriteText("INIT: " + resourceLoader.step + "/22");
		//if (radarLog != null) radarLog.WriteText("INIT: " + resourceLoader.step + "/11");
		return;
	}
	if (first)
	{
		first = false;
	}
	//if (gyroECU == null) gyroECU = new GyroECU5();
	var r = startitr;
	if (r < 21)
	{
		if (r == 0)
		{
			thrustScan();
			log("Sequential code activation...");
		}
		else if (r == 1) weaponDesync();
		else if (r == 2) updatePDCNets();
		else if (r == 3) railchargeupdate();
		else if (r == 4) updRadar(RDRSCT.OBS1);
		else if (r == 5) updRadar(RDRSCT.THRT1);
		else if (r == 6) updRadar(RDRSCT.OBS2);
		else if (r == 7) updRadar(RDRSCT.THRT2);
		else if (r == 8) updRadar(RDRSCT.THRT3);
		else if (r == 9) updRadar(RDRSCT.SPD);
		else if (r == 10) updRadar(RDRSCT.STRG);
		else if (r == 11) updRadar(RDRSCT.MVRS);
		else if (r == 12) updRadar(RDRSCT.DTCT);
		else if (r == 13) updRadar(RDRSCT.SPRITELCD);
		else if (r == 14) thrustScan();
		else if (r == 15) weaponDesync();
		else if (r == 16) updatePDCNets();
		else if (r == 17) railchargeupdate();
		else if (r == 18) grinder_update();
		else if (r == 19)
		{
			speedmatch_upd();
			autorot_update();
			gyroECU.update();
		}
		else if (r == 20)
		{
			log("JIT compile done.");
			log("POSTBOOT DONE. " + tick + "t (" + (((float)tick) / 60).ToString("0.0") + "s)", LT.LOG_N);
			log("avg ms/s:" + bt60.avg().ToString("0.0"));
			postboottick = tick + 20;
		}
		startitr++;
	}
	else
	{
		thrustScan();
		updRadar();
		weaponDesync();
		updatePDCNets();
		railchargeupdate();
		grinder_update();
		speedmatch_upd();
		autorot_update();
		gyroECU.update();
		ammoPull();
		fuelPull();
	}
	if (tick > postboottick)
	{
		log("Setting ms limit to " + MAX_MS_PER_SEC.ToString("0.00"));
		bt60.maxmspersec = MAX_MS_PER_SEC;
		postboottick = int.MaxValue;
	}



	if (arg == "resetConfig")
	{
		resetConfig();
		Me.CustomData = resourceLoader.lastCustomData = serializeConfig();
	}
	else if (arg.StartsWith("match")) speedMatch(arg);
	else if (arg == "lookat") toggleAutorotate(false);
	else if (arg == "rlookat") toggleAutorotate(true);
	else if (arg == "turnonall")
	{
		List<IMyFunctionalBlock> tmp = new List<IMyFunctionalBlock>();
		GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock>(tmp, b => b.IsSameConstructAs(Me) && !(b is IMyShipGrinder) && !(b is IMyShipWelder));
		foreach (var b in tmp) b.Enabled = true;
	}

	if (save_putdampon)
	{
		save_putdampon = false;
		getCtrl().DampenersOverride = true;
	}
}





class targetReserve
{
	public targetReserve(IMyFunctionalBlock we, long e, int ex)
	{
		w = we; eid = e; expire = ex;
	}
	public IMyFunctionalBlock w;
	public long eid;
	public int expire;
}

List<IMyFunctionalBlock> desynced = new List<IMyFunctionalBlock>();

List<targetReserve> reservations = new List<targetReserve>();

targetReserve targetReserved(long e)
{
	foreach (var r in reservations) if (r.eid == e) return r;
	return null;
}

static Profiler wpndsyncP = new Profiler("wpndsync");

void weaponDesync()
{
	wpndsyncP.s();
	for (int i = 0; i < reservations.Count;)
	{
		if (tick > reservations[i].expire) reservations.RemoveAt(i);
		else i++;
	}
	foreach (var w in desynced)
	{
		w.Enabled = true;
		APIWC.SetWeaponTarget(w, Me.EntityId, 0);
	}
	desynced.Clear();

	if (tick % 60 == 0)
	{
		foreach (var w in desyncGroup)
		{
			APIWC.SetWeaponTarget(w, Me.EntityId, 0);
			if (!desynced.Contains(w) && APIWC.IsWeaponReadyToFire(w))
			{
				var t = APIWC.GetWeaponTarget(w).GetValueOrDefault();
				if (t.Type == MyDetectedEntityType.LargeGrid || t.Type == MyDetectedEntityType.SmallGrid)
				{
					var reservetime = tick + (60 * 2) - 1;
					WeaponState ws = null;
					wsdict.TryGetValue(w, out ws);
					if (ws != null)
					{
						var apx_ttt = (w.GetPosition() - t.Position).Length() / ws.settings.ammoVel;
						reservetime = (int)(apx_ttt * 60) + 30;
					}

					var reserved = targetReserved(t.EntityId);
					if (reserved != null)
					{

						if (reserved.w != w)
						{
							w.Enabled = false;
							desynced.Add(w);
						}
						else reserved.expire = reservetime;
						//effectively, it won't count down until we actually fire
					}
					else
					{
						reservations.Add(new targetReserve(w, t.EntityId, reservetime));
					}
				}
			}
		}
	}
	wpndsyncP.e();
}


static Dictionary<Base6Directions.Direction, double> thrustByDir = new Dictionary<Base6Directions.Direction, double>();

static Dictionary<Base6Directions.Direction, List<IMyThrust>> thrustersByDir = new Dictionary<Base6Directions.Direction, List<IMyThrust>>();

static Dictionary<IMyThrust, double> thrusterMaxEffectiveThrust = new Dictionary<IMyThrust, double>();


static public List<IMyThrust> thrusters = new List<IMyThrust>();

static int tc = 0;

static public void thrustScan()
{
	bool rb = false;
	var t = _thrusters[tc];
	if (t.Enabled && t.IsFunctional && !t.Closed)
	{
		if (!thrusters.Contains(t))
		{
			thrusters.Add(t);
			rb = true;
		}
	}
	else
	{
		if (thrusters.Contains(t))
		{
			t.ThrustOverride = 0;
			thrusters.Remove(t);
			rb = true;
		}
	}
	tc = (tc + 1) % _thrusters.Count;
	if (rb)
	{
		updateMET();
		updateThrustByDir();
	}
}

static void updateMET()
{
	thrusterMaxEffectiveThrust.Clear();
	foreach (var t in thrusters)
	{
		var met = t.MaxEffectiveThrust;
		thrusterMaxEffectiveThrust[t] = met;
	}
}

static void updateThrustByDir()
{
	foreach (var d in Enum.GetValues(typeof(Base6Directions.Direction)))
	{
		var k = (Base6Directions.Direction)d;
		thrustByDir[k] = 0;
		if (!thrustersByDir.ContainsKey(k)) thrustersByDir[k] = new List<IMyThrust>();
		else thrustersByDir[k].Clear();
	}
	foreach (var t in thrusters)
	{
		//if (t.Enabled && t.IsFunctional)
		{
			var met = thrusterMaxEffectiveThrust[t];
			var f = t.Orientation.Forward;
			thrustByDir[f] += met;
			thrustersByDir[f].Add(t);
		}
	}
	var th = 0d;

	foreach (var kvp in thrustByDir)
	{
		if (kvp.Value > th)
		{
			th = kvp.Value;
			//thrustStrongest = kvp.Key;
		}
	}
}


List<MyItemType> accept = new List<MyItemType>();

public void ammoPull()
{
	if (tick % 60 == 0)
	{
		foreach (IMyTerminalBlock gun in weaponCoreWeapons)
		{
			for (var i = 0; i < gun.InventoryCount; i++)
			{
				var inv = gun.GetInventory(i);
				accept.Clear();
				inv.GetAcceptedItems(accept);
				if (accept.Count < 10)
				{
					foreach (var it in accept)
					{
						double v = (double)it.GetItemInfo().Volume;
						if (!inv.IsFull && (double)(inv.MaxVolume - inv.CurrentVolume + (MyFixedPoint)0.001) >= v)
						{

							int stock = 0;
							ammoInterface.items.TryGetValue(it, out stock);
							if (stock > 0)
							{
								bool success = ammoInterface.TransferItemTo(it, 9999, inv) != 9999;
							}
						}
					}
				}
			}
		}
	}
}

Dictionary<string, float> tankCap = new Dictionary<string, float> { { "MyObjectBuilder_Component/Fuel_Tank", 600000 }, { "MyObjectBuilder_Component/SG_Fuel_Tank", 4000 } };

void fuelPull()
{
	if (!Config.autoExtractor.Val) return;
	if (tick % 120 != 0) return;
	//string txt = "" + ld + "\n";
	double total_cap = 0;
	double current_avail = 0;
	foreach (var t in tanks_hydrogen)
	{
		total_cap += t.Capacity;
		current_avail += t.Capacity * t.FilledRatio;
	}
	if (total_cap < MathHelper.EPSILON) return;
	float cur_fil_ratio = (float)(current_avail / total_cap);
	//txt += current_avail + "/" + total_cap + "\n";
	//txt += cur_fil_ratio + "%\n";
	if (cur_fil_ratio < Config.feMinHydrogen.Val / 100.0d || current_avail < MathHelper.EPSILON)
	{
		//txt += "seek fuel\n";
		while (extractors.Count > 0 && (!extractors[0].IsFunctional || extractors[0].Closed)) extractors.RemoveAt(0);
		if (extractors.Count > 0)
		{
			var extractor = extractors[0];
			//txt += "found extractor\n";
			var einv = extractor.GetInventory(0);
			List<MyItemType> ai = new List<MyItemType>();
			einv.GetAcceptedItems(ai);
			//txt += "accepts:" + ai.Count + "\n";
			for (var i = 0; i < ai.Count; i++)
			{
				var tt = ai[i];
				var ttc = tankCap.ContainsKey(tt.TypeId) ? tankCap[tt.TypeId] : 600000;
				if (total_cap - current_avail > ttc || current_avail < MathHelper.EPSILON)
				{
					//txt += "a\n";
					if (!einv.ContainItems(1, tt))
					{
						//txt += "b\n";
						int mvd = ammoInterface.TransferItemTo(tt, 1, einv);
						if (mvd == 0)
						{
							//txt += "c\n";
							break;
						}
					}
				}
			}
		}
	}
}

public class Profiler
{


	static bool PROFILING_ENABLED = true;
	static List<Profiler> profilers = new List<Profiler>();
	const int mstracklen = 60;
	double[] mstrack = new double[mstracklen];
	double msdiv = 1.0d / mstracklen;
	int mscursor = 0;
	DateTime start_time = DateTime.MinValue;
	string Name = "";
	string pre = "";
	string post = "";
	int _ticks_between_calls = 1;
	int ltick = int.MinValue;
	//..int callspertick = 1;

	static int base_sort_position_c = 0;
	int base_sort_position = 0;

	bool nevercalled = true;
	//bool closed = true;
	public int getSortPosition()
	{
		if (nevercalled) return int.MaxValue;
		int mult = (int)Math.Pow(10, 8 - (depth * 2));
		if (parent != null) return parent.getSortPosition() + (base_sort_position * mult);
		return base_sort_position * mult;
	}
	static int basep = (int)Math.Pow(10, 5);
	public Profiler(string name)
	{
		if (PROFILING_ENABLED)
		{
			Name = name;
			profilers.Add(this);
			for (var i = 0; i < mstracklen; i++) mstrack[i] = 0;
			base_sort_position = base_sort_position_c;
			base_sort_position_c += 1;
		}
	}
	public void s()
	{
		start();
	}
	public void e()
	{
		stop();
	}
	static List<Profiler> stack = new List<Profiler>();
	Profiler parent = null;
	int depth = 0;
	bool adding = false;
	public void start()
	{
		if (PROFILING_ENABLED)
		{
			//closed = false;
			nevercalled = false;
			if (tick != ltick)
			{
				if (_ticks_between_calls == 1 && ltick != int.MinValue)
				{
					_ticks_between_calls = tick - ltick;
				}
				else
				{
					var tbc = tick - ltick;
					if (tbc != _ticks_between_calls)
					{
						_ticks_between_calls = 1;
						for (var i = 0; i < mstracklen; i++) mstrack[i] = 0;
					}
				}

				ltick = tick;
				//callspertick = 1;
				adding = false;
			}
			else
			{
				adding = true;
			}
			if (depth == 0) depth = stack.Count;
			if (depth > 11) depth = 11;
			if (stack.Count > 0 && parent == null) parent = stack[stack.Count - 1];
			stack.Add(this);
			start_time = DateTime.Now;
		}
	}
	double lastms = 0;
	double average = 0;


	/// <summary>
	/// records a fake ms consumption for this timeframe - for tests or demo
	/// </summary>
	public double FAKE_stop(double fakems)
	{
		return stop(fakems);
	}
	/// <summary>
	/// adds the elapsed time since start() to the records
	/// </summary>
	public double stop()
	{
		double time = 0;
		if (PROFILING_ENABLED)
		{
			//closed = true;
			time = (DateTime.Now - start_time).TotalMilliseconds;
		}
		return stop(time);
	}

	private double stop(double _ms)
	{
		double time = 0;
		if (PROFILING_ENABLED)
		{
			time = _ms;

			stack.Pop();
			if (parent != null)
			{
				depth = parent.depth + 1;
			}

			//if(!adding)mscursor = (mscursor + 1) % mstracklen;


			if (!adding) mstrack[mscursor] = 0;
			mstrack[mscursor] += time;
			if (!adding) mscursor = (mscursor + 1) % mstracklen;

			average = 0d;
			foreach (double ms in mstrack) average += ms;
			average *= msdiv;
			average /= _ticks_between_calls;
			lastms = time;
		}
		return time;
	}
	/// <summary>
	/// generates a monospaced report text. If called every tick, every 120 ticks it will recalculate treeview data.
	/// </summary>
	//the treeview can be initially inaccurate as some profilers might not be called every tick, depending on program architecture
	public string getReport(StringBuilder bu)
	{
		if (PROFILING_ENABLED)
		{
			if (tick % 120 == 25)//recalculate hacky treeview data, delayed by 25 ticks from program start
			{
				try
				{
					profilers.Sort(delegate (Profiler x, Profiler y)
					{
						return x.getSortPosition().CompareTo(y.getSortPosition());
					});
				}
				catch (Exception) { }

				for (int i = 0; i < profilers.Count; i++)
				{
					Profiler p = profilers[i];

					p.pre = "";
					if (p.depth > 0 && p.parent != null)
					{
						bool parent_has_future_siblings = false;
						bool has_future_siblings_under_parent = false;
						for (int b = i + 1; b < profilers.Count; b++)
						{
							if (profilers[b].depth == p.parent.depth) parent_has_future_siblings = true;
							if (profilers[b].depth == p.depth) has_future_siblings_under_parent = true;
							if (profilers[b].depth < p.depth) break;

						}
						while (p.pre.Length < p.parent.depth)
						{
							if (parent_has_future_siblings) p.pre += "│";
							else p.pre += " ";
						}
						bool last = false;

						if (!has_future_siblings_under_parent)
						{
							if (i < profilers.Count - 1)
							{
								if (profilers[i + 1].depth != p.depth) last = true;
							}
							else last = true;
						}
						if (last) p.pre += "└";
						else p.pre += "├";
						while (p.pre.Length < p.depth) p.pre += "─";
					}
				}
				int mlen = 0;
				foreach (Profiler p in profilers) if (p.pre.Length + p.Name.Length > mlen) mlen = p.pre.Length + p.Name.Length;
				foreach (Profiler p in profilers)
				{
					p.post = "";
					int l = p.pre.Length + p.Name.Length + p.post.Length;
					if (l < mlen) p.post = new string('_', mlen - l);
				}
			}
			if (nevercalled) bapp(bu, "!!!!", Name, "!!!!: NEVER CALLED!");
			else bapp(bu, pre, Name, post, ": ", lastms.ToString("0.00"), ";", average.ToString("0.00"));
		}
		return "";
	}
	static public string getAllReports()
	{
		StringBuilder b = new StringBuilder();
		//string r = "";
		if (PROFILING_ENABLED)
		{
			foreach (Profiler watch in profilers)
			{
				watch.getReport(b);
				b.Append("\n");
			}
		}
		if (stack.Count > 0)
		{
			bapp(b, "profile stack error:\n", stack.Count, "\n");
			foreach (var s in stack)
			{
				bapp(b, s.Name, ",");
			}
		}
		return b.ToString();
	}
}


class PDCNet
{
	public string label;
	public Base6Directions.Direction dir;
	public double range;

	int interval = 3;
	int[] heatavg5s = new int[60];//entry every 5 ticks?
	int heatc = 0;
	public PDCNet(string lbl)
	{
		label = lbl;
		for (int i = 0; i < 60; i++) heatavg5s[i] = 0;
	}
	public List<PDCFirmware> PDCs = new List<PDCFirmware>();
	public void addPDC(PDCFirmware b)
	{
		PDCs.Add(b);
		PDCFirmwareList.Add(b);
	}
	public bool remPDC(IMyFunctionalBlock b)
	{
		PDCFirmware del = null;
		foreach (var w in PDCs)
		{
			if (w.b == b)
			{
				del = w;
				break;
			}
		}
		if (del != null)
		{
			PDCs.Remove(del);
			PDCFirmwareList.Remove(del);
			return true;
		}
		return false;
	}


	public void update()
	{
		if (tick % interval == 0)
		{
			int topheat = 0;
			int totalheat = 0;
			bool chng = false;
			foreach (PDCFirmware frm in PDCs)
			{
				if (frm.lUpdTick == tick)
				{
					//frm.update();
					int heat = (int)(frm.lastHeat * 100 / frm.settings.MaxHeat);
					totalheat += heat;
					if (heat > topheat) topheat = heat;
					chng = true;
				}
			}
			if (chng)
			{
				if (PDCs.Count > 0) totalheat /= PDCs.Count;
				topHeat = topheat;
				avgHeat = totalheat;
			}

			heatavg5s[heatc] = totalheat;
			heatc = (heatc + 1) % 60;
		}
	}
	public int topHeat = 0;
	public int avgHeat = 0;
	public int topheat3sago()
	{
		//as a rolling 5s loop the next entry is about 5 s ago
		return heatavg5s[(heatc + 1) % 60];
	}
}

class PDCNetGroup
{
	public string name = "";
	public bool sided = false;
	public List<PDCNet> PDCNets = new List<PDCNet>();
	public string bgroup = "";

	List<IMyFunctionalBlock> trackedPDC = new List<IMyFunctionalBlock>();
	public void addPDC(IMyFunctionalBlock b)
	{
		if (trackedPDC.Contains(b)) return;
		trackedPDC.Add(b);
		string label = new string(' ', dirl[0].Length);
		var d = Base6Directions.Direction.Forward;
		if (sided)
		{
			var ctrl = getCtrl();
			var w = ctrl.WorldMatrix;
			Vector3D dir = b.WorldMatrix.Up;
			if (dir == w.Forward || dir == w.Backward) dir = b.WorldMatrix.Forward;
			string cat = dirl[0];
			if (dir == w.Left) d = Base6Directions.Direction.Left;
			else if (dir == w.Right) d = Base6Directions.Direction.Right;
			else if (dir == w.Up) d = Base6Directions.Direction.Up;
			else if (dir == w.Down) d = Base6Directions.Direction.Down;
			label = dirl[(int)d];
		}
		PDCNet net = null;
		foreach (var n in PDCNets)
		{
			if (n.dir == d)
			{
				net = n;
				break;
			}
		}
		if (net == null)
		{
			net = new PDCNet(label);
			net.dir = d;
			PDCNets.Add(net);
		}
		net.addPDC(new PDCFirmware(b));
	}
	public void remPDC(IMyFunctionalBlock b)
	{
		if (!trackedPDC.Contains(b)) return;
		trackedPDC.Remove(b);
		foreach (var n in PDCNets) if (n.remPDC(b)) break;
	}

	public void update()
	{
		foreach (var n in PDCNets) n.update();
	}
}


static List<PDCNetGroup> PDCNetGroups = new List<PDCNetGroup>();

static void updPDCNetGroups()
{
	foreach (var kvp in PDCBlockGroups)
	{


		var t = kvp.Key.Split(' ');
		PDCNetGroup group = new PDCNetGroup();
		group.bgroup = kvp.Key;
		if (t.Length > 1) group.name = t[1];
		if (t.Length > 2 && t[2].ToLower() == "sided") group.sided = true;
		bool exist = false;
		foreach (var g in PDCNetGroups)
		{
			if (g.name == group.name && g.sided == group.sided && g.bgroup == group.bgroup)
			{
				exist = true;
				break;
			}
		}
		if (!exist)
		{
			PDCNetGroups.Add(group);
		}
	}
}


int curBG = 0;

int curPDC = 0;



public void PDCScan2()
{
	pdcscanP.s();
	if (tick % 10 == 0)
	{
		if (curBG >= PDCBlockGroupK.Count) curBG = curPDC = 0;
		if (PDCBlockGroupK.Count > 0)
		{
			var g = PDCBlockGroupK[curBG];
			var y = PDCBlockGroups[g];
			if (y.Count > 0)
			{
				var b = y[curPDC];
				if (b.IsFunctional && !b.Closed) addPDC(g, b);
				else remPDC(g, b);
			}
			curPDC = curPDC + 1;
			if (curPDC >= y.Count)
			{
				curPDC = 0;
				curBG = (curBG + 1) % PDCBlockGroupK.Count;
			}
		}
	}
	pdcscanP.e();
}

void addPDC(string bg, IMyFunctionalBlock b)
{
	foreach (var g in PDCNetGroups)
	{
		if (g.bgroup == bg)
		{
			g.addPDC(b);
			break;
		}
	}
}

void remPDC(string bg, IMyFunctionalBlock b)
{
	foreach (var g in PDCNetGroups)
	{
		if (g.bgroup == bg)
		{
			g.remPDC(b);
			break;
		}
	}
}


static List<PDCFirmware> PDCFirmwareList = new List<PDCFirmware>();


static Profiler pdcnetP = new Profiler("pdcnet");

static Profiler pdcupdP = new Profiler("upd");


static Profiler pdcupdhP = new Profiler("upd_h");

static Profiler pdcupdhaP = new Profiler("upd_ha");

void updatePDCNets()
{
	pdcnetP.s();
	PDCScan2();
	pdcupdP.s();
	{
		pdcupdhP.s();
		foreach (var frm in PDCFirmwareList) frm.update();
		//..foreach (var net in PDCNetGroups) foreach (var x in net.PDCNets) foreach (PDCFirmware frm in x.PDCs) frm.update();
		pdcupdhP.e();
	}
	pdcupdhaP.s();
	foreach (var net in PDCNetGroups) net.update();
	pdcupdhaP.e();
	pdcupdP.e();
	renderPDC();
	pdcnetP.e();
}

static Profiler pdcscanP = new Profiler("scn");


static string[] dirl = new string[]
{
	"  FWD",
	" BACK",
	" LEFT",
	"RIGHT",
	"  TOP",
	" DOWN"
};

SpriteHUDLCD PDCLogSprite = null;


void renderPDC()
{
	if (tick % 60 != 0 || PDCLog == null) return;

	if (PDCLog != null)
	{
		if (PDCLogSprite == null) PDCLogSprite = new SpriteHUDLCD(PDCLog);
		PDCLogSprite.s = PDCLog;
	}
	StringBuilder b = new StringBuilder();
	if (PDCBlockGroupK.Count != 0)
	{
		b.Append("<color=white>SIDE PD#  AVG/TOP\n");
		foreach (var netg in PDCNetGroups)
		{
			if (!netg.sided && netg.name.Length < 5) bapp(b, new string(' ', 5 - netg.name.Length));
			bapp(b, "<color=lightsteelblue>", netg.name);//, "\n");
			if (netg.sided || netg.name.Length > 5) bapp(b, "\n");

			foreach (var netk in netg.PDCNets)
			{
				var n = netk;
				var d = netk.dir;
				var labl = "";
				if (netg.sided) labl = dirl[(int)d];
				var dif = n.avgHeat - n.topheat3sago();
				var odif = dif;
				var sym = '↑';
				if (dif == 0)
				{
					sym = ' ';
				}
				else if (dif < 0)
				{
					sym = '↓';
				}
				if (dif < 0) dif = -dif;
				//⚠
				if (n.avgHeat > 80) b.Append("<color=indianred>");
				else if (n.avgHeat > 60) b.Append("<color=orangered>");
				else if (n.avgHeat > 35) b.Append("<color=goldenrod>");
				else if (n.avgHeat > 20) b.Append("<color=palegoldenrod>");
				else b.Append("<color=lightgray>");
				bapp(b, labl);
				if (n.PDCs.Count < 10) b.Append("  ");
				else b.Append(" ");
				bapp(b, n.PDCs.Count, " ");
				if (n.avgHeat < 10) b.Append("  ");
				else if (n.avgHeat < 100) b.Append(" ");
				bapp(b, n.avgHeat, "%/");
				if (n.topHeat < 10) b.Append("  ");
				else if (n.topHeat < 100) b.Append(" ");
				bapp(b, n.topHeat, "%");
				if (odif > 0) b.Append("<color=red>");
				else if (odif == 0) b.Append("<color=lightgray>");
				else if (odif < 0) b.Append("<color=lightgreen>");
				bapp(b, ' ', sym);
				bapp(b, dif, "%/3s\n");
			}
		}
	}
	PDCLogSprite.setLCD(b.ToString());
}

public enum LT
{
	LOG_N = 0,
	LOG_D,
	LOG_DD
}

string[] logtype_labels = { "INFO", "_DBG", "DDBG" };


public static LT LOG_LEVEL = LT.LOG_N;

public static Logger logger = new Logger();

public static void log(string s, LT level)
{
	Logger.log(s, level);
}

public static void log(string s)
{
	Logger.log(s, LT.LOG_N);
}


public class Logger
{
	public class logmsg
	{
		public logmsg(string m, string m2, LT l) { msg = m; msg_raw = m2; level = l; }
		public string msg = "";
		public string msg_raw = "";
		public int c = 1;
		public LT level = LT.LOG_N;
	}

	static List<logmsg> loggedMessages = new List<logmsg>();
	static int MAX_LOG = 50;
	static List<logmsg> superLoggedMessages = new List<logmsg>();
	static int MAX_SUPER_LOG = 500;

	static public bool loggedMessagesDirty = true;

	public static void log(string s, LT level)
	{
		if (level > LOG_LEVEL) return;
		string s2 = s;
		if (s.Length > 50)
		{
			List<string> tok = new List<string>();
			while (s.Length > 50)
			{
				int c = 0;
				if (tok.Count > 0) c = 2;
				tok.Add(s.Substring(0, 50 - c));
				s = s.Substring(50 - c);
			}
			tok.Add(s);
			s = string.Join("\n ", tok);
		}
		var p = gProgram;
		logmsg l = null;
		if (loggedMessages.Count > 0)
		{
			l = loggedMessages[loggedMessages.Count - 1];
		}
		if (l != null)
		{
			if (l.msg == s) l.c += 1;
			else loggedMessages.Add(new logmsg(s, s2, level));
		}
		else loggedMessages.Add(new logmsg(s, s2, level));
		if (loggedMessages.Count > MAX_LOG) loggedMessages.RemoveAt(0);

		l = null;
		if (superLoggedMessages.Count > 0)
		{
			l = superLoggedMessages[superLoggedMessages.Count - 1];
		}
		if (l != null)
		{
			if (l.msg == s) l.c += 1;
			else superLoggedMessages.Add(new logmsg(s, s2, level));
		}
		else superLoggedMessages.Add(new logmsg(s, s2, level));
		if (superLoggedMessages.Count > MAX_SUPER_LOG) superLoggedMessages.RemoveAt(0);

		loggedMessagesDirty = true;
	}


	static public string loggedMessagesRender = "";
	static public void updateLoggedMessagesRender()
	{
		if (!loggedMessagesDirty) return;
		StringBuilder b = new StringBuilder();
		//if (!loggedMessagesDirty) return;// loggedMessagesRender;


		foreach (var m in loggedMessages)
		{
			b.Append(m.msg);
			if (m.c > 1) bapp(b, " (", m.c, ")");
			b.Append("\n");
		}
		string o = b.ToString();
		loggedMessagesDirty = false;
		loggedMessagesRender = o;
	}
}

public class GyroECU5
{
	public static double gyroMaxRPM = Math.PI;

	public double angleMultiplier = 1;

	bool firstrun = true;

	public GyroECU5() : base()
	{
	}
	void init()
	{
		angleMultiplier = gProgram.Me.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Small ? 2 : 1;
		gyroMaxRPM *= angleMultiplier;
		firstrun = false;
	}


	double lastAngleRoll, lastAnglePitch, lastAngleYaw;
	double lMPTRoll, lMPTPitch, lMPTYaw;

	public bool active = false;
	Vector3D targetPosition;
	Vector3D targetVelocity;//currently unused

	Vector3D targetHeading;
	Vector3D targetUp;

	public int startTick;
	public int ticksOnTarget = 0;
	private void flush()
	{
		if (firstrun) init();
		if (!active)
		{
			active = true;
			foreach (var g in gyros) g.GyroOverride = false;
			lG = null;
			startTick = tick;
			lastAngleRoll = lastAnglePitch = lastAngleYaw = 0;
			lMPTRoll = lMPTPitch = lMPTYaw = 0;
			ticksOnTarget = 0;
		}
	}

	public void rotateToPosition(Vector3D tp, Vector3D tv = new Vector3D())
	{
		if (!active) log("GECU test init rtp", LT.LOG_N);
		flush();
		targetPosition = tp;
		targetVelocity = tv;
		targetUp = getCtrl().WorldMatrix.Up;
	}
	public void rotateToHeading(Vector3D forward, Vector3D up = new Vector3D())
	{
		if (up == Vector3D.Zero) up = getCtrl().WorldMatrix.Up;
		if (!active)
		{
			log("GECU test init rth", LT.LOG_N);
			log("forward:" + v2ss(forward), LT.LOG_N);
			log("up:" + v2ss(up), LT.LOG_N);
		}
		flush();
		targetPosition = targetVelocity = Vector3D.Zero;
		targetHeading = forward;
		targetUp = up;
	}

	double error_thresholdLocked = ConvertDegreesToRadians(1);//we must be within this much on each axis
	double minVelThresholdLocked = ConvertDegreesToRadians(1d);
	//these only set qualifiers for ticksOnTarget.

	private void calculateAxisSpecificData(double now, ref double prior, ref double lastMPT, out bool braking, out double lastMPTActual, string p = "")//, out double ticksToStop, out double ticksToTarget)
	{

		var radMovedPerTick = Math.Abs(prior - now);
		var ticksToTarget = Math.Abs(now) / radMovedPerTick;
		var initVel = radMovedPerTick;
		var rateOfDecel = Math.Abs(lastMPT - radMovedPerTick);
		//if (rateOfDecel > mod) mod = rateOfDecel;

		//if (Math.Abs(now) > nobrake_threshold) rateOfDecel *= 1.5;//overestimating here did not improve timings
		var ticksToStop = initVel / rateOfDecel;
		//mod - maximum observed decel - saved 0.1s on large sluggish ship but lost .3s on sg snappy ship.
		//sticking to the conservative metric

		bool closing = Math.Abs(now) < Math.Abs(prior);
		lastMPTActual = radMovedPerTick;
		if (!closing)
		{
			lastMPT = 0.0001;
		}
		else lastMPT = radMovedPerTick;

		if (closing)
		{
			if (ticksToStop > ticksToTarget + 1) braking = true;
			else braking = false;
		}
		else braking = false;

		prior = now;
	}

	//for distances under 90 degrees, it returns a value between 0 and gyromaxRPM, sharpened with sine so it levels off a bit in a nice way at the end.
	//slightly black magic, but if it works, it works
	static double amp_threshold = ConvertDegreesToRadians(100);//125.0d);
	static double deAmp(double i)//, double spd)
	{
		if (i == 0) return i;
		var abs = Math.Abs(i);
		var ig = i / abs * gyroMaxRPM;
		//spd = 0;
		if (abs > amp_threshold) return ig;

		i = i / (amp_threshold);
		i = Math.Abs(i);
		i = Math.Sin(i);
		return i * ig;
	}

	public static void setUpdateRate(int ups)
	{
		updateRate = ups;
		updr_mult = 60 / updateRate;
		updr_mult_div = updr_mult / 60.0d;
	}

	static int updateRate = 30;
	static int updr_mult = 60 / updateRate;
	static double updr_mult_div = 60.0d / updateRate / 60.0d;
	int ltick = -1;
	public void update()
	{

		if (active && tick - ltick > updr_mult)// && tick % 2 == 0)
		{
			ltick = tick;

			if (!targetPosition.IsZero()) targetHeading = Vector3D.Normalize(targetPosition - getPosition());

			double pitch, yaw, roll;
			GetRotationAnglesSimultaneousDedup(targetHeading, targetUp, getCtrl().WorldMatrix, out yaw, out pitch, out roll);

			double rA, pA, yA;

			bool yB, pB, rB;
			calculateAxisSpecificData(roll, ref lastAngleRoll, ref lMPTRoll, out rB, out rA);
			calculateAxisSpecificData(pitch, ref lastAnglePitch, ref lMPTPitch, out pB, out pA);
			calculateAxisSpecificData(yaw, ref lastAngleYaw, ref lMPTYaw, out yB, out yA);

			//Vector3D a_act = new Vector3D(pA, yA,rA);
			//var amax = a_act.AbsMax();

			Vector3D a_impulse = new Vector3D(pB ? 0 : pitch, yB ? 0 : yaw, rB ? 0 : roll);

			//black magic everywhere
			a_impulse.X = deAmp(a_impulse.X);//, amax * updr_mult);
			a_impulse.Y = deAmp(a_impulse.Y);//, amax * updr_mult);
			a_impulse.Z = deAmp(a_impulse.Z);//, amax * updr_mult);
			if (Math.Abs(pA) / 60 * updateRate > Math.Abs(a_impulse.X)) a_impulse.X = 0;
			if (Math.Abs(yA) / 60 * updateRate > Math.Abs(a_impulse.Y)) a_impulse.Y = 0;
			if (Math.Abs(rA) / 60 * updateRate > Math.Abs(a_impulse.Z)) a_impulse.Z = 0;


			GyroOverride(getCtrl().WorldMatrix, a_impulse.X, a_impulse.Y, a_impulse.Z);

			if (Math.Abs(roll) < error_thresholdLocked && Math.Abs(pitch) < error_thresholdLocked && Math.Abs(yaw) < error_thresholdLocked)
			{
				if (rA < minVelThresholdLocked * updr_mult_div && pA < minVelThresholdLocked * updr_mult_div && yA < minVelThresholdLocked * updr_mult_div)
				{
					ticksOnTarget += 1;
				}
				else ticksOnTarget = 0;
			}
			else ticksOnTarget = 0;
		}
	}

	IMyGyro lG = null;
	static Vector3D state = new Vector3D(9, 9, 9);
	const double E = MathHelper.EPSILON;
	void GyroOverride(MatrixD shipRef, double pitch_speed, double yaw_speed, double roll_speed)
	{
		IMyGyro g = null;
		foreach (var c in gyros)
		{
			if (c.Enabled && c.IsFunctional && !c.Closed)
			{
				g = c;
				break;
			}
		}
		if (g == null) return;

		if (g != lG)
		{
			if (lG != null) lG.GyroOverride = false;
			lG = g;
			state = new Vector3D(9, 9, 9);
			g.GyroOverride = true;
		}

		var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
		var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipRef);
		var trv = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(g.WorldMatrix));

		if (trv.X != state.X)// && Math.Abs(trv.X - state.X) > E)
		{
			state.X = trv.X;
			g.Pitch = (float)(state.X);
		}
		if (trv.Y != state.Y)// && Math.Abs(trv.Y - state.Y) > E)
		{
			state.Y = trv.Y;
			g.Yaw = (float)(state.Y);
		}
		if (trv.Z != state.Z)// && Math.Abs(trv.Z - state.Z) > E)
		{
			state.Z = trv.Z;
			g.Roll = (float)(state.Z);
		}
	}

	public void shutdown()
	{
		if (active)
		{
			active = false;
			if (lG != null) lG.GyroOverride = false;
			log("GECU shutdown", LT.LOG_N);
			log("time: " + (tick - startTick) + "t (" + ((double)(tick - startTick) / 60).ToString("0.0") + "s)", LT.LOG_N);
		}
	}
}


bool gsetup = false;


static public List<string> discard_components = new List<string>();

static public List<string> keepfilters = new List<string>();

static string dcl = "";

static string kfl = "";

static void filterchk()
{
	if (Config.EjectDiscard.Val != dcl)
	{
		dcl = Config.EjectDiscard.Val;
		discard_components = new List<string>(dcl.Split(','));
	}
	if (Config.EjectKeep.Val != kfl)
	{
		kfl = Config.EjectKeep.Val;
		keepfilters = new List<string>(kfl.Split(','));
	}
}

bool ejecting = true;

public void grinder_update()
{
	grinderInterface.updateInterval = 30;
	grinderInterface.update();

	//gProgram.Echo(gProgram.grinders.Count + ":" + gProgram.connectors.Count);
	if (tick % 60 == 0)
	{

		if (grinders.Count > 0 && connectors.Count > 0)
		{
			if (!gsetup)
			{
				gsetup = true;
				foreach (var g in grinders)
				{
					g.UseConveyorSystem = !Config.Eject.Val;
				}
			}
			if (Config.Eject.Val)
			{
				var grinders_on = grinders.Count > 0 ? grinders[0].Enabled : false;

				if (grinders_on)
				{
					filterchk();
					foreach (var kvp in grinderInterface.items)
					{
						var i = kvp.Key.GetItemInfo();
						/*
						 if ammo or ingot or ore or tool, keep.
						otherwise, if in vanilla comp list, trash
						unless it has wildcards in it, then keep anyway.
						 */
						bool keep = i.IsAmmo || i.IsIngot || i.IsOre || i.IsTool;
						if (!keep) keep = !discard_components.Contains(kvp.Key.SubtypeId);
						if (!keep)
						{
							foreach (var f in keepfilters)
							{
								if (kvp.Key.SubtypeId.IndexOf(f) != -1)
								{
									keep = true;
									break;
								}
							}
						}
						if (keep) grinderInterface.TransferItemTo(kvp.Key, kvp.Value, lootInterface);
						else grinderInterface.TransferItemTo(kvp.Key, kvp.Value, connectorInterface);
					}
				}
				connectorInterface.update(grinders_on);
				bool shouldEject = connectorInterface.items.Count > 0;
				if (shouldEject)
				{
					foreach (var c in connectors)
					{
						if (c.IsConnected)
						{
							shouldEject = false;
							break;
						}
					}
				}
				if (shouldEject != ejecting)
				{
					ejecting = shouldEject;
					foreach (var c in connectors)
					{
						c.ThrowOut = ejecting;
					}
				}
			}
		}
	}
}

public class Config_
{
	public Setting<bool> autoExtractor = new Setting<bool>("autoExtractor").Default(true)
	;//.Desc("Misc - Automatically feed fuel items to extractors to maintain a minimum fuel % in tanks");
	public Setting<int> feMinHydrogen = new Setting<int>("feMinHydrogen").Default(10)
	;//.Desc("Misc - Percentage, 0-100, that determines when to feed fuel items if feedExtractors is enabled.");
	 //public Setting<int> feMinHydrogenDock = new Setting<int>("feMinHydrogenDock").Default(50)
	 //;//.Desc("Misc - Percentage, 0-100, that it will try to pull from another grid if docked to it.");

	public Setting<bool> Eject = new Setting<bool>("Eject").Default(true);
	public Setting<string> EjectDiscard = new Setting<string>("EjectDiscardFilter").Default("BulletproofGlass,Canvas,Computer,Construction,Detector,Display,Explosives,Girder,GravityGenerator,InteriorPlate,LargeTube,Medical,MetalGrid,Motor,PowerCell,RadioCommunication,Reactor,SmallTube,SolarCell,SteelPlate,Superconductor,Thrust,AmmoCache,Fuel_Tank,HeavyArms,SmallArms,ToolPack,SemiAutoPistolMagazine");
	public Setting<string> EjectKeep = new Setting<string>("EjectKeepFilter").Default("MCRN,UNN,Belter,Adv,Upg,Experi,Lidar,Black,Data");

	public Setting<string> WeaponDesync = new Setting<string>("WeaponDesyncBlockGroup").Default("Railguns");

	public Setting<string> PDCGroup = new Setting<string>("PDCHeatBlockGroup").Default("PDCs");
	//public Setting<bool> PDCAntiOverheat = new Setting<bool>("PDCAntiOverheat").Default(true);


	public Setting<string> RSubtarget = new Setting<string>("RSubtargBlockGroup").Default("Railguns");
	public Setting<string> RFC = new Setting<string>("RFriendColor").Default("lightgreen");
	public Setting<string> REC = new Setting<string>("REnemyColor").Default("red");
	public Setting<string> RNC = new Setting<string>("RNeutColor").Default("lightgreen");
	public Setting<string> ROC = new Setting<string>("ROtherColor").Default("gray");

	//public Setting<bool> PDCSeparateRange = new Setting<bool>("PDCSeparateRange").Default(true);
}

static Config_ Config = null;

DetectedEntity rotateTarget = null;

bool autoRotate = false;

bool AR_reverse = false;

void toggleAutorotate(bool reverse = false)
{
	AR_reverse = reverse;
	autoRotate = !autoRotate;
	if (autoRotate)
	{
		rotateTarget = null;
		MyDetectedEntityInfo? focus = APIWC.GetAiFocus(Me.CubeGrid.EntityId);
		if (focus.HasValue) detectedEntitiesD.TryGetValue(focus.Value.EntityId, out rotateTarget);
		if (rotateTarget == null && matchTarget != null) rotateTarget = matchTarget;

		if (rotateTarget == null) autoRotate = false;
	}
	else
	{
		rotateTarget = null;
		gyroECU.shutdown();
	}
	renderMatch();
}

double steppedPredictive = 0;

bool lastahead = false;

int ticksAB = 0;

void autorot_update()
{
	if (autoRotate)
	{
		bool canTrack = false;
		if (rotateTarget != null)
		{
			DetectedEntity d = null;
			detectedEntitiesD.TryGetValue(rotateTarget.EntityId, out d);
			if (d != null)
			{
				canTrack = true;
				rotateTarget = d;
			}
			MyDetectedEntityInfo? focus = APIWC.GetAiFocus(Me.CubeGrid.EntityId);
			if (focus.HasValue)
			{
				if (focus.Value.EntityId == rotateTarget.EntityId) rotateTarget.upd(focus.Value);
			}

			if (rotateTarget != null) rotateTarget.upd(focus.Value);

			if (!canTrack || rotateTarget == null)
			{
				toggleAutorotate();
			}
		}
		if (rotateTarget != null)
		{
			var pos = getPosition();
			var tpos = rotateTarget.getEstPos();
			var offset = (tpos - pos);
			var dist = offset.Length();
			Vector3D aim = offset / dist;
			if (AR_reverse) aim = -aim;
			gyroECU.rotateToHeading(aim);
		}
	}
}

static public double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
	if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
		return 0;
	else
		return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
}

static int getCtrlTick = -9000;

static IMyShipController getCtrlL = null;

static IMyShipController getCtrl()
{
	if (tick - getCtrlTick > 3 * 60)
	{
		foreach (var c in controllers)
		{
			if (c.IsUnderControl)
			{
				getCtrlL = c;
				break;
			}
		}
		if (getCtrlL == null)
		{
			foreach (var c in controllers)
			{
				if (c.IsMainCockpit)
				{
					getCtrlL = c;
					break;
				}
			}
		}
		if (getCtrlL == null && controllers.Count > 0) getCtrlL = controllers[0];
		getCtrlTick = tick;
	}
	return getCtrlL;
}



static int getMassT = -1;

static double getMassL = 0;

static double getMass()
{
	if (tick != getMassT)
	{
		getMassT = tick;
		getMassL = getCtrl().CalculateShipMass().PhysicalMass;
	}
	return getMassL;
}

static int getPositionT = -1;

static Vector3D getPositionL = Vector3D.Zero;

static Vector3D getPosition()
{
	if (tick != getPositionT)
	{
		getPositionL = getCtrl().GetPosition();
		getPositionT = tick;
	}
	return getPositionL;
}

static int getVelocityT = -1;

static Vector3D getVelocityL = Vector3D.Zero;

static Vector3D getVelocity()
{
	if (tick != getVelocityT)
	{
		getVelocityL = getCtrl().GetShipVelocities().LinearVelocity;
		getVelocityT = tick;
	}
	return getVelocityL;
}

static int getGravityT = -1;

static Vector3D getGravityL = Vector3D.Zero;

static Vector3D getGravity()
{
	if (tick != getGravityT)
	{
		getGravityL = getCtrl().GetNaturalGravity();
		getGravityT = tick;
	}
	return getGravityL;
}


public class WcPbApi
{
	public string[] WcBlockTypeLabels = new string[]
		{
			"Any",
			"Offense",
			"Utility",
			"Power",
			"Production",
			"Thrust",
			"Jumping",
			"Steering"
		};

	private Action<ICollection<MyDefinitionId>> a;
	private Func<IMyTerminalBlock, IDictionary<string, int>, bool> b;
	private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> c;
	private Func<long, bool> d;
	private Func<long, int, MyDetectedEntityInfo> e;
	private Func<IMyTerminalBlock, long, int, bool> f;
	private Action<IMyTerminalBlock, bool, bool, int> g;
	private Func<IMyTerminalBlock, bool> h;
	private Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> i;
	private Func<IMyTerminalBlock, ICollection<string>, int, bool> j;
	private Action<IMyTerminalBlock, ICollection<string>, int> k;
	private Func<IMyTerminalBlock, long, int, Vector3D?> l;

	private Func<IMyTerminalBlock, int, Matrix> m;
	private Func<IMyTerminalBlock, int, Matrix> n;
	private Func<IMyTerminalBlock, long, int, MyTuple<bool, Vector3D?>> o;
	private Func<IMyTerminalBlock, int, string> p;
	private Action<IMyTerminalBlock, int, string> q;
	private Func<long, float> r;
	private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> s;
	private Action<IMyTerminalBlock, long, int> t;
	private Func<long, MyTuple<bool, int, int>> u;

	private Action<IMyTerminalBlock, bool, int> v;
	private Func<IMyTerminalBlock, int, bool, bool, bool> w;
	private Func<IMyTerminalBlock, int, float> x;
	private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> y;
	private Func<IMyTerminalBlock, float> _getCurrentPower;
	public Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _getHeatLevel;

	public bool isReady = false;
	IMyTerminalBlock pbBlock = null;
	public bool Activate(IMyTerminalBlock pbBlock)
	{
		this.pbBlock = pbBlock;
		var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
		if (dict == null) throw new Exception("WcPbAPI failed to activate");
		return ApiAssign(dict);
	}

	public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
	{
		if (delegates == null)
			return false;
		AssignMethod(delegates, "GetCoreWeapons", ref a);
		AssignMethod(delegates, "GetBlockWeaponMap", ref b);
		AssignMethod(delegates, "GetSortedThreats", ref c);
		AssignMethod(delegates, "GetObstructions", ref i);
		AssignMethod(delegates, "HasGridAi", ref d);
		AssignMethod(delegates, "GetAiFocus", ref e);
		AssignMethod(delegates, "SetAiFocus", ref f);
		AssignMethod(delegates, "HasCoreWeapon", ref h);
		AssignMethod(delegates, "GetPredictedTargetPosition", ref l);
		AssignMethod(delegates, "GetTurretTargetTypes", ref j);
		AssignMethod(delegates, "SetTurretTargetTypes", ref k);
		AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref m);
		AssignMethod(delegates, "GetWeaponElevationMatrix", ref n);
		AssignMethod(delegates, "IsTargetAlignedExtended", ref o);
		AssignMethod(delegates, "GetActiveAmmo", ref p);
		AssignMethod(delegates, "SetActiveAmmo", ref q);
		AssignMethod(delegates, "GetConstructEffectiveDps", ref r);
		AssignMethod(delegates, "GetWeaponTarget", ref s);
		AssignMethod(delegates, "SetWeaponTarget", ref t);
		AssignMethod(delegates, "GetProjectilesLockedOn", ref u);

		AssignMethod(delegates, "FireWeaponOnce", ref v);
		AssignMethod(delegates, "ToggleWeaponFire", ref g);
		AssignMethod(delegates, "IsWeaponReadyToFire", ref w);
		AssignMethod(delegates, "GetMaxWeaponRange", ref x);
		AssignMethod(delegates, "GetWeaponScope", ref y);

		AssignMethod(delegates, "GetCurrentPower", ref _getCurrentPower);
		AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);

		//Delegate.CreateDelegate(null, null);

		isReady = true;
		return true;
	}
	private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
	{
		if (delegates == null)
		{
			field = null;
			return;
		}
		Delegate del;
		if (!delegates.TryGetValue(name, out del))
			throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
		field = del as T;
		if (field == null)
			throw new Exception(
				$"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
	}

	public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => a?.Invoke(collection);
	public void GetSortedThreats(IDictionary<MyDetectedEntityInfo, float> collection) =>
		c?.Invoke(pbBlock, collection);
	public bool HasGridAi(long entity) => d?.Invoke(entity) ?? false;
	public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => e?.Invoke(shooter, priority);

	public bool SetAiFocus(IMyTerminalBlock pBlock, long target, int priority = 0) =>
		f?.Invoke(pBlock, target, priority) ?? false;

	public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
		g?.Invoke(weapon, on, allWeapons, weaponId);
	public bool HasCoreWeapon(IMyTerminalBlock weapon) => h?.Invoke(weapon) ?? false;

	public void GetObstructions(IMyTerminalBlock pBlock, ICollection<MyDetectedEntityInfo> collection) =>
		i?.Invoke(pBlock, collection);

	public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
		l?.Invoke(weapon, targetEnt, weaponId) ?? null;

	public Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weapon, int weaponId) =>
		m?.Invoke(weapon, weaponId) ?? Matrix.Zero;

	public Matrix GetWeaponElevationMatrix(IMyTerminalBlock weapon, int weaponId) =>
		n?.Invoke(weapon, weaponId) ?? Matrix.Zero;

	public MyTuple<bool, Vector3D?> IsTargetAlignedExtended(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
		o?.Invoke(weapon, targetEnt, weaponId) ?? new MyTuple<bool, Vector3D?>();
	public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
		p?.Invoke(weapon, weaponId) ?? null;

	public void SetActiveAmmo(IMyTerminalBlock weapon, int weaponId, string ammoType) =>
		q?.Invoke(weapon, weaponId, ammoType);

	public float GetConstructEffectiveDps(long entity) => r?.Invoke(entity) ?? 0f;

	public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
		s?.Invoke(weapon, weaponId);

	public void SetWeaponTarget(IMyTerminalBlock weapon, long target, int weaponId = 0) =>
		t?.Invoke(weapon, target, weaponId);

	public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
		b?.Invoke(weaponBlock, collection) ?? false;

	public MyTuple<bool, int, int> GetProjectilesLockedOn(long victim) =>
		u?.Invoke(victim) ?? new MyTuple<bool, int, int>();

	public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
			v?.Invoke(weapon, allWeapons, weaponId);


	public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
		bool shootReady = false) =>
		w?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

	public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
		x?.Invoke(weapon, weaponId) ?? 0f;

	public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
		y?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();
	public float GetCurrentPower(IMyTerminalBlock weapon) => _getCurrentPower?.Invoke(weapon) ?? 0f;

	public float GetHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
}

static public List<PDCData> PDCDatalist = new List<PDCData>();

static public Dictionary<string, PDCData> PDCDataSubType = new Dictionary<string, PDCData>();

static void wsinit()
{
	if (PDCDatalist.Count > 0) return;
	new PDCData("OPA Point Defence Cannon", 1200, 100, 36000, 320, true);
	new PDCData("OPA Shotgun PDC", 240, 500, 36000, 320, true);
	new PDCData("OPA Shotgun PDC Angled", 240, 500, 36000, 320, true);
	new PDCData("Ostman-Jazinski Flak Cannon", 900, 100, 18000, 160, true);
	new PDCData("Voltaire Collective Anti Personnel PDC", 600, 100, 9000, 80, true);
	new PDCData("Nariman Dynamics PDC", 1800, 100, 45000, 400, true);
	new PDCData("Nariman Dynamics PDC Angled", 1800, 100, 45000, 400, true);
	new PDCData("Redfields Ballistics PDC", 1200, 90, 45000, 400, true);
	new PDCData("Redfields Ballistics PDC Angled", 1200, 90, 45000, 400, true);

	new RailData("UNN MA-15 Coilgun", 420, 900.0000f, 120, 6000.0f);
	new RailData("Mounted Zakosetara Heavy Railgun", 420, 3300.0000f, 120, 8000.0f);
	new RailData("OPA Behemoth Heavy Railgun", 300, 4202.6667f, 120, 12500.0f);
	new RailData("T-47 Roci Light Fixed Railgun", 324, 2400.0000f, 36, 9000.0f);
	new RailData("Zakosetara Heavy Railgun", 540, 3300.0000f, 0, 8000.0f);
	new RailData("Dawson-Pattern Medium Railgun", 300, 3000.0000f, 120, 10000.0f);
	new RailData("Farren-Pattern Heavy Railgun", 300, 4800.0000f, 120, 12500.0f);
	new RailData("V-14 Stiletto Light Railgun", 300, 2700.0000f, 120, 10000.0f);
	new RailData("VX-12 Foehammer Ultra-Heavy Railgun", 300, 4500.0000f, 120, 12500.0f);
	//wsreport();
}

static void wsreport()
{
	foreach (var n in PDCDatalist)
	{
		log(n.SubTypeId + ": " + n.indefiniteShotBurstDelay, LT.LOG_N);
	}
}



public class PDCData
{
	public string SubTypeId;

	public int RateOfFire = 0;
	public int HeatPerShot = 0;
	public int MaxHeat = 1;
	public int HeatSinkRate = 0;
	public bool DegradeRof = false;

	public int minTicksPerShot = 60;
	public int shotsPerSecIndefinite;
	public int indefiniteShotBurstDelay = 0;
	public int ticksPerShotBurst = 0;
	public int ticksPerShotBursth = 0;

	public double lowerROFHeatThreshold = 0;

	public ITerminalProperty<float> bdTProp = null;
	public ITerminalProperty<float> wrTProp = null;


	public PDCData(string n, int rof = 0, int hps = 0, int mh = 0, int hsr = 0, bool drof = false)
	{
		SubTypeId = n;
		PDCDatalist.Add(this);
		PDCDataSubType[SubTypeId] = this;

		RateOfFire = rof;
		HeatPerShot = hps;
		MaxHeat = mh;
		HeatSinkRate = hsr;
		DegradeRof = drof;
		/*if (DegradeRof) lowerROFHeatThreshold = (MaxHeat * 0.8) - (HeatPerShot * 4);
		else*/
		lowerROFHeatThreshold = MaxHeat - (HeatPerShot * 3);
		if (HeatPerShot != 0 && HeatSinkRate > 0)
		{//should probably precompile this since I'm precompiling so much else, but can't be assed right now
			try
			{
				double shotspersecburst = rof / 60.0d;
				var heatgen = HeatPerShot * shotspersecburst;
				var indefrof = rof * HeatSinkRate / heatgen;
				double shotspersecindefinite = indefrof / 60.0d;
				double tickspShotIndefinite = 60 / shotspersecindefinite;
				double tickspShotBurst = 60 / shotspersecburst;
				double bd = tickspShotIndefinite - tickspShotBurst;
				indefiniteShotBurstDelay = (int)Math.Ceiling(bd) + 1;
				ticksPerShotBurst = (int)Math.Ceiling(tickspShotBurst);
				ticksPerShotBursth = ticksPerShotBurst / 2;
			}
			catch (Exception) { }
		}
	}


	public string report()
	{
		return SubTypeId + ":" + shotsPerSecIndefinite + "," + indefiniteShotBurstDelay;
	}


	bool needCalc = true;
	public void calc(PDCFirmware f)
	{
		if (needCalc)
		{
			needCalc = false;
			try
			{
				//bdTProp = f.b.GetProperty("Burst Delay").AsFloat();
				wrTProp = f.b.GetProperty("Weapon Range").AsFloat();
			}
			catch (Exception) { }
		}
	}
}


public class PDCFirmware
{
	public IMyFunctionalBlock b = null;
	public PDCData settings = null;
	public float range = 0;
	int offset = 0;
	static int offsetitr = 0;

	public PDCFirmware(IMyFunctionalBlock block)
	{
		b = block;
		PDCDataSubType.TryGetValue(b.DefinitionDisplayNameText, out settings);
		if (settings == null) settings = new PDCData(b.DefinitionDisplayNameText);
		settings.calc(this);
		range = settings.wrTProp.GetValue(b);
		offset = offsetitr++;
	}

	public int updateRate()
	{
		var updateRate = settings.ticksPerShotBurst;
		//..var updOff = offset;
		return updateRate;
	}

	public int lUpdTick = -1;

	public double lastHeat = 0;
	public void update()//this function should be called every tick.
	{
		var u = updateRate();
		if (u == 0) u = 5;
		//if (settings.ticksPerShotBurst < offsetitr) updOff = offset * offsetitr / settings.ticksPerShotBurst;
		if (tick % u == 0)// || lastHeat > settings.MaxHeat*0.8)
		{
			lastHeat = APIWC.GetHeatLevel(b);
			//setEnable(!Config.PDCAntiOverheat.Val || lastHeat < settings.lowerROFHeatThreshold);
			lUpdTick = tick;
		}
	}
	bool laste = false;
	public void setEnable(bool e)
	{

		if (laste != e)
		{
			b.Enabled = e;
			laste = e;
		}

	}


	int lastBurstDelay = -1;
	public void setBurstDelay(int d)
	{

		if (lastBurstDelay != d)
		{
			if (settings.bdTProp != null)
			{
				lastBurstDelay = d;
				settings.bdTProp.SetValue(b, (long)d);
			}
		}
	}
}


static public List<RailData> RailDatalist = new List<RailData>();

static public Dictionary<string, RailData> RailDataSubType = new Dictionary<string, RailData>();

public class RailData
{
	public string SubTypeId;
	public int chargeTicks = 0;
	public int DUF = 0;
	public float maxDraw = 0;
	public float ammoVel = 0;
	public RailData(string subtype, int ticks, float maxcharge, int duf, float vel)
	{
		SubTypeId = subtype;
		RailDatalist.Add(this);
		RailDataSubType[SubTypeId] = this;
		chargeTicks = ticks;
		maxDraw = maxcharge;
		DUF = duf;
		ammoVel = vel;
	}
}




class WeaponState
{
	public IMyTerminalBlock b = null;
	public RailData settings = null;
	public bool isCharging = false;

	public float chargeProgress = 0;
	public void setCharging(bool b)
	{
		if (b != isCharging)
		{
			isCharging = b;
			if (b) chargeProgress = 0;
			else chargeProgress = 1;
		}
	}
	float lDraw = 0;
	float lProg = 0;
	public float lastDrawFactor = 0;
	public void update()
	{
		if (lDraw == 0 || tick % 3 == 0)
		{
			lDraw = APIWC.GetCurrentPower(b);
			setCharging(lDraw > 5);
		}
		if (isCharging)
		{
			if (tick % 3 == 0)
			{
				lastDrawFactor = lDraw / settings.maxDraw;
				lProg = 1.0f / settings.chargeTicks * lastDrawFactor;
			}
			chargeProgress += lProg;
			if (chargeProgress > 1)
			{
				chargeProgress = 1;
			}
		}
	}
}

Dictionary<IMyTerminalBlock, WeaponState> wsdict = new Dictionary<IMyTerminalBlock, WeaponState>();

WeaponState getWS(IMyTerminalBlock b)
{
	WeaponState ws = null;
	wsdict.TryGetValue(b, out ws);
	if (ws == null)
	{
		if (RailDataSubType.ContainsKey(b.DefinitionDisplayNameText))
		{
			ws = wsdict[b] = new WeaponState();
			ws.settings = RailDataSubType[b.DefinitionDisplayNameText];
			ws.b = b;
		}
	}
	return ws;
}

static Profiler railP = new Profiler("rail");

public void railchargeupdate()
{
	railP.s();
	foreach (var w in subsystemTargeters)
	{
		WeaponState ws = null;
		wsdict.TryGetValue(w, out ws);
		if (ws == null)
		{
			if (RailDataSubType.ContainsKey(w.DefinitionDisplayNameText))
			{
				ws = wsdict[w] = new WeaponState();
				ws.settings = RailDataSubType[w.DefinitionDisplayNameText];
				ws.b = w;
			}
		}
		if (ws == null) continue;
		ws.update();
	}
	railP.e();
}
