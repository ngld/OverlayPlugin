'use strict';

const defaultLang = 'English';
const languages = [
  'Chinese',
  'English',
  'French',
  'German',
  'Korean',
  'Japanese',
];

const languageToLocale = {
  English: 'en-US',
  Chinese: 'zh-CN',
};

// Map of language -> targetType -> settings title.
const configTitles = {
  English: {
    Target: 'Target Settings',
    Focus: 'Focus Target Settings',
    Hover: 'Hover Target Settings',
    TargetOfTarget: 'Target of Target Settings',
  },
  Chinese: {
    Target: '目标 - 设置',
    Focus: '焦点目标 - 设置',
    Hover: '悬停目标 - 设置',
    TargetOfTarget: '目标的目标 - 设置',
  },
};

const helpText = {
  English: '(🔒lock overlay to hide settings)',
  Chinese: '(🔒锁定悬浮窗以隐藏设置)',
};

// language -> displayed option text -> text key
const textOptionsAll = {
  English: {
    'None': 'None',
    'Current HP': 'CurrentHP',
    'Max HP': 'MaxHP',
    'Current / Max HP': 'CurrentAndMaxHP',
    'Percent HP': 'PercentHP',
    'Time To Death': 'TimeToDeath',
    'Distance': 'Distance',
    'Effective Distance': 'EffectiveDistance',
    'Absolute Enmity': 'AbsoluteEnmity',
    'Relative Enmity': 'RelativeEnmity',
  },
  Chinese: {
    '不显示': 'None',
    '当前体力值': 'CurrentHP',
    '最大体力值': 'MaxHP',
    '当前体力值 / 最大体力值': 'CurrentAndMaxHP',
    '体力值百分比': 'PercentHP',
    '推测死亡时间': 'TimeToDeath',
    '目标距离': 'Distance',
    '有效距离': 'EffectiveDistance',
    '绝对仇恨': 'AbsoluteEnmity',
    '相对仇恨': 'RelativeEnmity',
  },
};


const overlayDataKey = 'targetbars';
const targets = ['Target', 'Focus', 'Hover', 'TargetOfTarget'];

// Values that come directly from a target object.
const rawKeys = ['CurrentHP', 'MaxHP', 'Distance', 'EffectiveDistance'];
// Values that need to be calculated.
const otherKeys = ['PercentHP', 'CurrentAndMaxHP', 'TimeToDeath'];
// Values that only exist for the current Target.
const targetOnlyKeys = ['AbsoluteEnmity', 'RelativeEnmity'];

const validKeys = ['None', ...rawKeys, ...otherKeys, ...targetOnlyKeys];

// Remove enmity from non-target keys.
const textOptionsNonTarget = (() => {
  let options = {};
  for (const [lang, perLang] of Object.entries(textOptionsAll)) {
    options[lang] = {};
    for (const key in perLang) {
      const value = perLang[key];
      if (targetOnlyKeys.includes(value))
        continue;
      options[lang][key] = value;
    }
  }
  return options;
})();

const textOptionsByTargetType = {
  Target: textOptionsAll,
  Focus: textOptionsNonTarget,
  Hover: textOptionsNonTarget,
  TargetOfTarget: textOptionsNonTarget,
};

const FormatType = {
  Raw: 0,
  Separators: 1,
  Simplify3: 2,
  Simplify4: 3,
  Simplify5: 4,
};

const formatOptionsByKey = {
  CurrentHP: {
    maximumFractionDigits: 0,
  },
  MaxHP: {
    maximumFractionDigits: 0,
  },
  Distance: {
    minimumFractionDigits: 2,
  },
  EffectiveDistance: {
    maximumFractionDigits: 0,
  },
  PercentHP: {
    minimumFractionDigits: 2,
  },
  CurrentAndMaxHP: {
    maximumFractionDigits: 0,
  },
  TimeToDeath: {
    maximumFractionDigits: 0,
  },
  AbsoluteEnmity: {
    maximumFractionDigits: 0,
  },
  RelativeEnmity: {
    maximumFractionDigits: 0,
  },
};

// Auto-generate number formatting options.
// Adjust the formatNumber function to make this behave differently per lang
// language -> displayed option text -> text key
const formatOptions = (() => {
  const defaultValue = 123456789;
  const defaultKey = 'CurrentHP';
  let formatOptions = {};
  for (const lang of languages) {
    let obj = {};
    for (const typeName in FormatType) {
      const type = FormatType[typeName];
      obj[formatNumber(defaultValue, lang, type, defaultKey)] = type;
    }

    formatOptions[lang] = obj;
  }
  return formatOptions;
})();

const configStructure = [
  {
    id: 'leftText',
    name: {
      English: 'Left Text',
      Chinese: '左侧文本',
    },
    type: 'select',
    optionsByType: textOptionsByTargetType,
    default: 'CurrentAndMaxHP',
  },
  {
    id: 'middleText',
    name: {
      English: 'Middle Text',
      Chinese: '中间文本',
    },
    optionsByType: textOptionsByTargetType,
    type: 'select',
    default: 'Distance',
  },
  {
    id: 'rightText',
    name: {
      English: 'Right Text',
      Chinese: '右侧文本',
    },
    optionsByType: textOptionsByTargetType,
    type: 'select',
    default: 'PercentHP',
  },
  {
    id: 'barHeight',
    name: {
      English: 'Height of the bar',
      Chinese: '血条高度',
    },
    type: 'text',
    default: 11,
  },
  {
    id: 'barWidth',
    name: {
      English: 'Width of the bar',
      Chinese: '血条宽度',
    },
    type: 'text',
    default: 250,
  },
  {
    // TODO: maybe there's a desire to format left/mid/right differently?
    id: 'numberFormat',
    name: {
      English: 'Number Format',
      Chinese: '数字格式',
    },
    type: 'select',
    options: formatOptions,
    default: FormatType.Separators,
  },
  {
    id: 'isRounded',
    name: {
      English: 'Enable rounded corners',
      Chinese: '视觉效果 - 圆角',
    },
    type: 'checkbox',
    default: true,
  },
  {
    id: 'borderSize',
    name: {
      English: 'Size of the border',
      Chinese: '描边宽度',
    },
    type: 'text',
    default: 1,
  },
  {
    id: 'borderColor',
    name: {
      English: 'Color of the border',
      Chinese: '描边颜色',
    },
    type: 'text',
    default: 'black',
  },
  {
    id: 'fontSize',
    name: {
      English: 'Size of the font',
      Chinese: '字体大小',
    },
    type: 'text',
    default: 10,
  },
  {
    id: 'fontFamily',
    name: {
      English: 'Name of the font',
      Chinese: '字体名称',
    },
    type: 'text',
    default: 'Meiryo',
  },
  {
    id: 'fontColor',
    name: {
      English: 'Color of the font',
      Chinese: '字体颜色',
    },
    type: 'text',
    default: 'white',
  },
  {
    id: 'bgColor',
    name: {
      English: 'Background depleted bar color',
      Chinese: '血条背景色',
    },
    type: 'text',
    default: 'rgba(4, 15, 4, 1)',
  },
  {
    id: 'fgColorHigh',
    name: {
      English: 'Bar color when hp is high',
      Chinese: '血条颜色 - 高血量',
    },
    type: 'text',
    default: 'rgba(0, 159, 1, 1)',
  },
  {
    id: 'midColorPercent',
    name: {
      English: 'Percent below where hp is mid',
      Chinese: '中血量定义 (剩余体力值百分比)',
    },
    type: 'text',
    default: 60,
  },
  {
    id: 'fgColorMid',
    name: {
      English: 'Bar color when hp is mid',
      Chinese: '血条颜色 - 中血量',
    },
    type: 'text',
    default: 'rgba(160, 130, 30, 1)',
  },
  {
    id: 'lowColorPercent',
    name: {
      English: 'Percent below where hp is mid',
      Chinese: '低血量定义 (剩余体力值百分比)',
    },
    type: 'text',
    default: 30,
  },
  {
    id: 'fgColorLow',
    name: {
      English: 'Bar color when hp is low',
      Chinese: '血条颜色 - 低血量',
    },
    type: 'text',
    default: 'rgba(240, 40, 30, 1)',
  },
];

const perTargetOverrides = {
  Target: {
    barWidth: 415,
    leftText: 'CurrentAndMaxHP',
    middleText: 'TimeToDeath',
    rightText: 'Distance',
  },
  Focus: {
    barWidth: 210,
    leftText: 'CurrentAndMaxHP',
    middleText: 'None',
    rightText: 'PercentHP',
  },
};

// Return "str px" if "str" is a number, otherwise "str".
function defaultAsPx(str) {
  if (parseFloat(str) == str)
    return str + 'px';
  return str;
}

// Simplifies a number to number of |digits|.
// e.g. num=123456789, digits=3 => 123M
// e.g. num=123456789, digits=4 => 123.4M
// e.g. num=-0.1234567, digits=3 => -0.123
function formatNumberSimplify(num, lang, options, digits) {
  // The leading zero does not count.
  if (num < 1)
    digits++;

  // Digits before the decimal.
  let originalDigits = Math.max(Math.floor(Math.log10(num)), 0) + 1;
  let separator = Math.floor((originalDigits - 1) / 3) * 3;

  // TODO: translate these too?
  let suffix = {
    0: '',
    3: 'k',
    6: 'M',
    9: 'B',
    12: 'T',
    15: 'Q',
  }[separator];

  num /= Math.pow(10, separator);

  let finalDigits = originalDigits - separator;
  // At least give 3 digits here even if requesting 2.
  let decimalPlacesNeeded = Math.max(digits - finalDigits, 0);

  // If this is a real decimal place, bound by the per-key formatting options.
  if (separator === 0) {
    if (typeof options.minimumFractionDigits !== 'undefined')
      decimalPlacesNeeded = Math.max(options.minimumFractionDigits, decimalPlacesNeeded);
    if (typeof options.maximumFractionDigits !== 'undefined')
      decimalPlacesNeeded = Math.min(options.maximumFractionDigits, decimalPlacesNeeded);
  }

  let shift = Math.pow(10, decimalPlacesNeeded);
  num = Math.floor(num * shift) / shift;

  const locale = languageToLocale[lang] || languageToLocale[defaultLang];
  return num.toLocaleString(locale, {
    minimumFractionDigits: decimalPlacesNeeded,
    maximumFractionDigits: decimalPlacesNeeded,
  }) + suffix;
}

function formatNumber(num, lang, format, key) {
  let floatNum = parseFloat(num);
  if (isNaN(floatNum))
    return num;
  num = floatNum;

  const options = formatOptionsByKey[key];
  const minDigits = options.minimumFractionDigits > 0 ? options.minimumFractionDigits : 0;
  const locale = languageToLocale[lang] || languageToLocale[defaultLang];

  switch (parseInt(format)) {
  default:
  case FormatType.Raw:
    return num.toFixed(minDigits);

  case FormatType.Separators:
    return num.toLocaleString(locale, options);

  case FormatType.Simplify3:
    return formatNumberSimplify(num, lang, options, 3);

  case FormatType.Simplify4:
    return formatNumberSimplify(num, lang, options, 4);

  case FormatType.Simplify5:
    return formatNumberSimplify(num, lang, options, 5);
  }
}

class BarUI {
  constructor(targetType, topLevelOptions, div, lang) {
    this.target = targetType;
    this.options = topLevelOptions[targetType];
    this.div = div;
    this.lang = lang;
    this.lastData = {};
    this.isExampleShowcase = false;
    this.targetHistory = new TargetHistory();

    // Map of keys to elements that contain those values.
    // built from this.options.elements.
    this.elementMap = {};

    const textMap = {
      left: this.options.leftText,
      center: this.options.middleText,
      right: this.options.rightText,
    };

    for (const [justifyKey, text] of Object.entries(textMap)) {
      if (!validKeys.includes(text)) {
        console.error(`Invalid key: ${text}`);
        continue;
      }

      let textDiv = document.createElement('div');
      textDiv.classList.add(text);
      textDiv.style.justifySelf = justifyKey;
      this.div.appendChild(textDiv);
      this.elementMap[text] = this.elementMap[text] || [];
      this.elementMap[text].push(textDiv);
    }

    if (this.options.isRounded)
      this.div.classList.add('rounded');
    else
      this.div.classList.remove('rounded');

    // TODO: could move settings container down by height of bar
    // but up to some maximum so it's not hidden if you type in
    // a ridiculous number, vs the absolute position it is now.
    this.div.style.height = defaultAsPx(this.options.barHeight);
    this.div.style.width = defaultAsPx(this.options.barWidth);

    let borderStyle = defaultAsPx(this.options.borderSize);
    borderStyle += ' solid ' + this.options.borderColor;
    this.div.style.border = borderStyle;

    this.div.style.fontSize = defaultAsPx(this.options.fontSize);
    this.div.style.fontFamily = this.options.fontFamily;
    this.div.style.color = this.options.fontColor;

    // Alignment hack:
    // align-self:center doesn't work when children are taller than parents.
    // TODO: is there some better way to do this?
    const containerHeight = parseInt(this.div.clientHeight);
    for (const el in this.elementMap) {
      for (let div of this.elementMap[el]) {
        // Add some text to give div a non-zero height.
        div.innerText = 'XXX';
        let divHeight = div.clientHeight;
        div.innerText = '';
        if (divHeight <= containerHeight)
          continue;
        div.style.position = 'relative';
        div.style.top = defaultAsPx((containerHeight - divHeight) / 2.0);
      }
    }
  }

  // EnmityTargetData event handler.
  update(e) {
    if (!e)
      return;

    if (!this.isExampleShowcase && e.isExampleShowcase)
      this.targetHistory = new ExampleTargetHistory();


    // Don't let the game updates override the example showcase.
    if (this.isExampleShowcase && !e.isExampleShowcase)
      return;
    this.isExampleShowcase = e.isExampleShowcase;

    let data = e[this.target];
    // If there's no target, or if the target is something like a marketboard
    // which has zero HP, then don't show the overlay.
    if (!data || data.MaxHP === 0) {
      this.setVisible(false);
      return;
    }

    for (const key of rawKeys) {
      if (data[key] === this.lastData[key])
        continue;

      const formatted = formatNumber(data[key], this.lang, this.options.numberFormat, key);
      this.setValue(key, formatted);
    }

    if (data.CurrentHP !== this.lastData.CurrentHP ||
        data.MaxHP !== this.lastData.MaxHP) {
      const percentKey = 'PercentHP';
      const percentOptions = formatOptionsByKey[percentKey];
      const percent = 100 * data.CurrentHP / data.MaxHP;
      const percentStr =
          formatNumber(percent, this.lang, this.options.numberFormat, percentKey) + '%';
      this.setValue('PercentHP', percentStr);
      this.updateGradient(percent);

      const comboKey = 'CurrentAndMaxHP';
      const formattedHP =
          formatNumber(data.CurrentHP, this.lang, this.options.numberFormat, comboKey);
      const formattedMaxHP =
          formatNumber(data.MaxHP, this.lang, this.options.numberFormat, comboKey);
      this.setValue(comboKey, formattedHP + ' / ' + formattedMaxHP);
    }

    // Time to death
    this.targetHistory.processTarget(data);
    let secondsRemaining = this.targetHistory.secondsUntilDeath(data);
    let ttd = secondsRemaining === null ? '' : toTimeString(secondsRemaining, this.lang);
    const ttdKey = 'TimeToDeath';
    data[ttdKey] = ttd;
    if (data[ttdKey] !== this.lastData[ttdKey])
      this.setValue(ttdKey, ttd);

    // Target enmity.
    if (this.target === 'Target') {
      const relKey = 'RelativeEnmity';
      const absKey = 'AbsoluteEnmity';
      let player = updateRelativeEnmity(e);
      if (player === null) {
        data[relKey] = '';
        data[absKey] = '';
      } else {
        data[absKey] = player.Enmity + '%';

        // Negative relative enmity has a minus, so add a plus for positive.
        let rel = player.RelativeEnmity;
        data[relKey] = (rel > 0 ? '+' : '') + rel + '%';
      }

      if (data[relKey] !== this.lastData[relKey])
        this.setValue(relKey, data[relKey]);
      if (data[absKey] !== this.lastData[absKey])
        this.setValue(absKey, data[absKey]);
    }

    this.lastData = data;
    this.setVisible(true);
  }

  updateGradient(percent) {
    // Find the colors from options, based on current percentage.
    let fgColor;
    if (percent > this.options.midColorPercent)
      fgColor = this.options.fgColorHigh;
    else if (percent > this.options.lowColorPercent)
      fgColor = this.options.fgColorMid;
    else
      fgColor = this.options.fgColorLow;

    // Right-fill with fgcolor up to percent, and then bgcolor after that.
    const bgColor = this.options.bgColor;
    let style = 'linear-gradient(90deg, ' +
      fgColor + ' ' + percent + '%, ' + bgColor + ' ' + percent + '%)';
    this.div.style.background = style;
  }

  setValue(name, value) {
    let nodes = this.elementMap[name];
    if (!nodes)
      return;
    for (let node of nodes)
      node.innerText = value;
  }

  setVisible(isVisible) {
    if (isVisible)
      this.div.classList.remove('hidden');
    else
      this.div.classList.add('hidden');
  }
}

class SettingsUI {
  constructor(targetType, lang, configStructure, savedConfig, settingsDiv, rebuildFunc) {
    this.savedConfig = savedConfig || {};
    this.div = settingsDiv;
    this.rebuildFunc = rebuildFunc;
    this.lang = lang;
    this.target = targetType;

    this.buildUI(settingsDiv, configStructure);

    rebuildFunc(savedConfig);
  }

  // Top level UI builder, builds everything.
  buildUI(container, configStructure) {
    container.appendChild(this.buildHeader());
    container.appendChild(this.buildHelpText());
    for (const opt of configStructure) {
      let buildFunc = {
        checkbox: this.buildCheckbox,
        select: this.buildSelect,
        text: this.buildText,
      }[opt.type];
      if (!buildFunc) {
        console.error('unknown type: ' + JSON.stringify(opt));
        continue;
      }

      buildFunc.bind(this)(container, opt, this.target);
    }
  }

  buildHeader() {
    let div = document.createElement('div');
    const titles = this.translate(configTitles);
    div.innerHTML = titles[this.target];
    div.classList.add('settings-title');
    return div;
  }

  buildHelpText() {
    let div = document.createElement('div');
    div.innerHTML = this.translate(helpText);
    div.classList.add('settings-helptext');
    return div;
  }

  // Code after this point in this class is largely cribbed from cactbot's
  // ui/config/config.js CactbotConfigurator.
  // If this gets used again, maybe it should be abstracted.

  async saveConfigData() {
    await callOverlayHandler({
      call: 'saveData',
      key: overlayDataKey,
      data: this.savedConfig,
    });
    this.rebuildFunc(this.savedConfig);
  }

  // Helper translate function.  Takes in an object with locale keys
  // and returns a single entry based on available translations.
  translate(textObj) {
    if (textObj === null || typeof textObj !== 'object' || !textObj[defaultLang])
      return textObj;
    let t = textObj[this.lang];
    if (t)
      return t;
    return textObj[defaultLang];
  }

  // takes variable args, with the last value being the default value if
  // any key is missing.
  // e.g. (foo, bar, baz, 5) with {foo: { bar: { baz: 3 } } } will return
  // the value 3.  Requires at least two args.
  getOption() {
    let num = arguments.length;
    if (num < 2) {
      console.error('getOption requires at least two args');
      return;
    }

    let defaultValue = arguments[num - 1];
    let objOrValue = this.savedConfig;
    for (let i = 0; i < num - 1; ++i) {
      objOrValue = objOrValue[arguments[i]];
      if (typeof objOrValue === 'undefined')
        return defaultValue;
    }

    return objOrValue;
  }

  // takes variable args, with the last value being the 'value' to set it to
  // e.g. (foo, bar, baz, 3) will set {foo: { bar: { baz: 3 } } }.
  // requires at least two args.
  setOption() {
    let num = arguments.length;
    if (num < 2) {
      console.error('setOption requires at least two args');
      return;
    }

    // Set keys and create default {} if it doesn't exist.
    let obj = this.savedConfig;
    for (let i = 0; i < num - 2; ++i) {
      let arg = arguments[i];
      obj[arg] = obj[arg] || {};
      obj = obj[arg];
    }
    // Set the last key to have the final argument's value.
    obj[arguments[num - 2]] = arguments[num - 1];
    this.saveConfigData();
  }

  buildNameDiv(opt) {
    let div = document.createElement('div');
    div.innerHTML = this.translate(opt.name);
    div.classList.add('option-name');
    return div;
  }

  buildCheckbox(parent, opt, group) {
    let div = document.createElement('div');
    div.classList.add('option-input-container');

    let input = document.createElement('input');
    div.appendChild(input);
    input.type = 'checkbox';
    input.checked = this.getOption(group, opt.id, opt.default);
    input.onchange = () => this.setOption(group, opt.id, input.checked);

    parent.appendChild(this.buildNameDiv(opt));
    parent.appendChild(div);
  }

  // <select> inputs don't work in overlays, so make a fake one.
  buildSelect(parent, opt, group) {
    let div = document.createElement('div');
    div.classList.add('option-input-container');
    div.classList.add('select-container');

    // Build the real select so we have a real input element.
    let input = document.createElement('select');
    input.classList.add('hidden');
    div.appendChild(input);

    const defaultValue = this.getOption(group, opt.id, opt.default);
    input.onchange = () => this.setOption(group, opt.id, input.value);

    const optionsByType = opt.optionsByType ? opt.optionsByType[this.target] : opt.options;
    const innerOptions = this.translate(optionsByType);
    for (const [key, value] of Object.entries(innerOptions)) {
      let elem = document.createElement('option');
      elem.value = value;
      elem.innerHTML = key;
      if (value === defaultValue)
        elem.selected = true;
      input.appendChild(elem);
    }

    parent.appendChild(this.buildNameDiv(opt));
    parent.appendChild(div);

    // Now build the fake select.
    let selectedDiv = document.createElement('div');
    selectedDiv.classList.add('select-active');
    selectedDiv.innerHTML = input.options[input.selectedIndex].innerHTML;
    div.appendChild(selectedDiv);

    let items = document.createElement('div');
    items.classList.add('select-items', 'hidden');
    div.appendChild(items);

    selectedDiv.addEventListener('click', (e) => {
      items.classList.toggle('hidden');
    });

    // Popout list of options.
    for (let idx = 0; idx < input.options.length; ++idx) {
      let optionElem = input.options[idx];
      let item = document.createElement('div');
      item.classList.add('select-item');
      item.innerHTML = optionElem.innerHTML;
      items.appendChild(item);

      item.addEventListener('click', (e) => {
        input.selectedIndex = idx;
        input.onchange();
        selectedDiv.innerHTML = item.innerHTML;
        items.classList.toggle('hidden');
        selectedDiv.classList.toggle('select-arrow-active');
      });
    }
  }

  buildText(parent, opt, group, step) {
    let div = document.createElement('div');
    div.classList.add('option-input-container');

    let input = document.createElement('input');
    div.appendChild(input);
    input.type = 'text';
    if (step)
      input.step = step;
    input.value = this.getOption(group, opt.id, opt.default);
    let setFunc = () => this.setOption(group, opt.id, input.value);
    input.onchange = setFunc;
    input.oninput = setFunc;

    parent.appendChild(this.buildNameDiv(opt));
    parent.appendChild(div);
  }
}

function updateOverlayState(e) {
  let settingsContainer = document.getElementById('settings-container');
  if (!settingsContainer)
    return;
  const locked = e.detail.isLocked;
  if (locked) {
    settingsContainer.classList.add('hidden');
    document.body.classList.remove('resize-background');
  } else {
    settingsContainer.classList.remove('hidden');
    document.body.classList.add('resize-background');
  }
  OverlayPluginApi.setAcceptFocus(!locked);
}

function showExample(barUI) {
  barUI.update({
    Entries: [
      {
        isMe: true,
        isCurrentTarget: true,
        Enmity: 100,
        RelativeEnmity: 100,
        Name: 'Tank',
        Job: 'PLD',
        MaxHP: 100,
        CurrentHP: 3,
      },
    ],
    Target: {
      Name: 'TargetMob',
      CurrentHP: 38300,
      MaxHP: 50000,
      Distance: 12.8,
      EffectiveDistance: 7,
    },
    Focus: {
      Name: 'FocusMob',
      CurrentHP: 8123,
      MaxHP: 29123,
      Distance: 52.7,
      EffectiveDistance: 45,
    },
    Hover: {
      Name: 'HoverMob',
      CurrentHP: 2300,
      MaxHP: 2500,
      Distance: 5.2,
      EffectiveDistance: 1,
    },
    TargetOfTarget: {
      Name: 'TargetOfTargetMob',
      CurrentHP: 15123,
      MaxHP: 32748,
      Distance: 12.6,
      EffectiveDistance: 3,
    },
    isExampleShowcase: true,
  });
}

// This event comes early and doesn't depend on any other state.
// So, add the listener before DOMContentLoaded.
document.addEventListener('onOverlayStateUpdate', updateOverlayState);

window.addEventListener('DOMContentLoaded', async (e) => {
  // Initialize language from OverlayPlugin.
  let lang = defaultLang;
  const langResult = await window.callOverlayHandler({ call: 'getLanguage' });
  if (langResult && langResult.language)
    lang = langResult.language;

  // Determine the type of target bar by a specially named container.
  let containerDiv;
  let targetType;
  for (const key of targets) {
    containerDiv = document.getElementById('container-' + key.toLowerCase());
    if (containerDiv) {
      targetType = key;
      break;
    }
  }
  if (!containerDiv) {
    console.error('Missing container');
    return;
  }

  // Set option defaults from config.
  let options = {};
  options[targetType] = {};
  for (const opt of configStructure)
    options[targetType][opt.id] = opt.default;

  // Handle per target default overrides.
  const overrides = perTargetOverrides[targetType];
  for (const key in overrides)
    options[targetType][key] = overrides[key];

  // Overwrite options from loaded values.  Options are stored once per target type,
  // so that different targets can be configured differently.
  const loadResult = await window.callOverlayHandler({ call: 'loadData', key: overlayDataKey });
  if (loadResult && loadResult.data)
    options = Object.assign(options, loadResult.data);

  // Creating settings will build the initial bars UI.
  // Changes to settings rebuild the bars.
  let barUI;
  let settingsDiv = document.getElementById('settings');
  let buildFunc = (options) => {
    while (containerDiv.lastChild)
      containerDiv.removeChild(containerDiv.lastChild);
    barUI = new BarUI(targetType, options, containerDiv, lang);
  };
  let gSettingsUI = new SettingsUI(targetType, lang, configStructure, options,
      settingsDiv, buildFunc);

  window.addOverlayListener('EnmityTargetData', (e) => barUI.update(e));
  document.addEventListener('onExampleShowcase', () => showExample(barUI));
  window.startOverlayEvents();
});
