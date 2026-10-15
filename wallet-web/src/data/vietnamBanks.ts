/**
 * Danh sách ngân hàng Việt Nam
 */

export interface BankInfo {
  code: string;
  name: string;
  shortName: string;
  logo: string;
  bin?: string[]; // Bank Identification Number
}

export const VIETNAM_BANKS: BankInfo[] = [
  {
    code: 'VCB',
    name: 'Ngân hàng TMCP Ngoại thương Việt Nam',
    shortName: 'Vietcombank',
    logo: '🏦',
    bin: ['970436']
  },
  {
    code: 'TCB',
    name: 'Ngân hàng TMCP Kỹ thương Việt Nam',
    shortName: 'Techcombank',
    logo: '🏦',
    bin: ['970407']
  },
  {
    code: 'VTB',
    name: 'Ngân hàng TMCP Việt Nam Thịnh Vượng',
    shortName: 'VPBank',
    logo: '🏦',
    bin: ['970432']
  },
  {
    code: 'BIDV',
    name: 'Ngân hàng TMCP Đầu tư và Phát triển Việt Nam',
    shortName: 'BIDV',
    logo: '🏦',
    bin: ['970418']
  },
  {
    code: 'VIB',
    name: 'Ngân hàng TMCP Quốc tế Việt Nam',
    shortName: 'VIB',
    logo: '🏦',
    bin: ['970441']
  },
  {
    code: 'MB',
    name: 'Ngân hàng TMCP Quân đội',
    shortName: 'MB Bank',
    logo: '🏦',
    bin: ['970422']
  },
  {
    code: 'ACB',
    name: 'Ngân hàng TMCP Á Châu',
    shortName: 'ACB',
    logo: '🏦',
    bin: ['970416']
  },
  {
    code: 'TPB',
    name: 'Ngân hàng TMCP Tiên Phong',
    shortName: 'TPBank',
    logo: '🏦',
    bin: ['970423']
  },
  {
    code: 'STB',
    name: 'Ngân hàng TMCP Sài Gòn Thương Tín',
    shortName: 'Sacombank',
    logo: '🏦',
    bin: ['970403']
  },
  {
    code: 'HDB',
    name: 'Ngân hàng TMCP Phát triển TP.HCM',
    shortName: 'HDBank',
    logo: '🏦',
    bin: ['970437']
  },
  {
    code: 'SHB',
    name: 'Ngân hàng TMCP Sài Gòn - Hà Nội',
    shortName: 'SHB',
    logo: '🏦',
    bin: ['970443']
  },
  {
    code: 'EIB',
    name: 'Ngân hàng TMCP Xuất Nhập khẩu Việt Nam',
    shortName: 'Eximbank',
    logo: '🏦',
    bin: ['970431']
  },
  {
    code: 'MSB',
    name: 'Ngân hàng TMCP Hàng Hải',
    shortName: 'MSB',
    logo: '🏦',
    bin: ['970426']
  },
  {
    code: 'VAB',
    name: 'Ngân hàng TMCP Việt Á',
    shortName: 'VietABank',
    logo: '🏦',
    bin: ['970427']
  },
  {
    code: 'NAB',
    name: 'Ngân hàng TMCP Nam Á',
    shortName: 'Nam A Bank',
    logo: '🏦',
    bin: ['970428']
  },
  {
    code: 'OCB',
    name: 'Ngân hàng TMCP Phương Đông',
    shortName: 'OCB',
    logo: '🏦',
    bin: ['970448']
  },
  {
    code: 'SEA',
    name: 'Ngân hàng TMCP Đông Nam Á',
    shortName: 'SeABank',
    logo: '🏦',
    bin: ['970440']
  },
  {
    code: 'ABB',
    name: 'Ngân hàng TMCP An Bình',
    shortName: 'ABBANK',
    logo: '🏦',
    bin: ['970425']
  },
  {
    code: 'VAB',
    name: 'Ngân hàng Nông nghiệp và Phát triển Nông thôn VN',
    shortName: 'Agribank',
    logo: '🏦',
    bin: ['970405']
  },
  {
    code: 'BAB',
    name: 'Ngân hàng TMCP Bắc Á',
    shortName: 'Bac A Bank',
    logo: '🏦',
    bin: ['970409']
  }
];

// Test card numbers (for development only)
export const TEST_CARD_NUMBERS = {
  visa: '4532015112345678',
  mastercard: '5425233430109903',
  amex: '378282246310005',
  jcb: '3530111333300000'
};
