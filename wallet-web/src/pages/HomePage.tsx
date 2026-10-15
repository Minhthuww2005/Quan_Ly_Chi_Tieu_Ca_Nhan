import { useState, useEffect } from 'react';
import { Shield, Zap, Users, ArrowRight, Star, ChevronLeft, ChevronRight, CreditCard, TrendingUp, Award } from 'lucide-react';
import { Button } from '../components/ui/button';
import LoginModal from '../components/LoginModal';
import RegisterModal from '../components/RegisterModal';
import ForgotPasswordModal from '../components/ForgotPasswordModal';

export default function HomePage() {
  const [activeModal, setActiveModal] = useState<'login' | 'register' | 'forgot-password' | null>(null);
  const [currentFeature, setCurrentFeature] = useState(0);
  const [currentTestimonial, setCurrentTestimonial] = useState(0);

  const features = [
    {
      icon: <CreditCard className="w-8 h-8" />,
      title: "Quản lý thẻ thông minh",
      description: "Tích hợp và quản lý tất cả thẻ ngân hàng trong một ứng dụng duy nhất",
      color: "from-blue-600 to-indigo-700"
    },
    {
      icon: <Zap className="w-8 h-8" />,
      title: "Giao dịch tức thời",
      description: "Chuyển tiền và thanh toán trong vòng vài giây với công nghệ blockchain",
      color: "from-emerald-600 to-teal-700"
    },
    {
      icon: <Shield className="w-8 h-8" />,
      title: "Bảo mật đa lớp",
      description: "Công nghệ mã hóa AES-256 và xác thực sinh trắc học tiên tiến",
      color: "from-violet-600 to-purple-700"
    }
  ];

  const testimonials = [
    {
      name: "Trịnh Minh Hậu và Đặng Hữu Hậu",
      role: "Các giám đốc kinh doanh",
      content: "HTD PAY đã cách mạng hóa cách chúng tôi quản lý tài chính doanh nghiệp. Hiệu quả và đáng tin cậy.",
      rating: 5
    },
    {
      name: "Nguyễn Trần Nhật Huy và Nguyễn Trọng Duy",
      role: "Các chuyên gia tài chính",
      content: "Giao diện trực quan, tính năng phong phú. Đây là ví điện tử tôi tin tùng sử dụng hàng ngày.",
      rating: 5
    },
    {
      name: "Lưu Cơ Thành và Nguyễn Ngọc Chí Thành",
      role: "Các Startup Founder",
      content: "Tích hợp dễ dàng, phí giao dịch thấp. HTD PAY là đối tác lý tưởng cho startup của chúng tôi.",
      rating: 5
    }
  ];

  const stats = [
    { number: "2M+", label: "Người dùng tin tưởng" },
    { number: "99.9%", label: "Thời gian hoạt động" },
    { number: "24/7", label: "Hỗ trợ khách hàng" },
    { number: "256-bit", label: "Mã hóa bảo mật" }
  ];

  // Auto slide features
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentFeature((prev) => (prev + 1) % features.length);
    }, 4000);
    return () => clearInterval(interval);
  }, []);

  // Auto slide testimonials
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTestimonial((prev) => (prev + 1) % testimonials.length);
    }, 5000);
    return () => clearInterval(interval);
  }, []);

  const nextFeature = () => {
    setCurrentFeature((prev) => (prev + 1) % features.length);
  };

  const prevFeature = () => {
    setCurrentFeature((prev) => (prev - 1 + features.length) % features.length);
  };

  const nextTestimonial = () => {
    setCurrentTestimonial((prev) => (prev + 1) % testimonials.length);
  };

  const prevTestimonial = () => {
    setCurrentTestimonial((prev) => (prev - 1 + testimonials.length) % testimonials.length);
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 text-slate-800 overflow-hidden">
      {/* Subtle Background Pattern */}
      <div className="absolute inset-0 overflow-hidden opacity-40">
        <div className="absolute top-0 left-1/4 w-72 h-72 bg-blue-200 rounded-full mix-blend-multiply filter blur-xl animate-pulse"></div>
        <div className="absolute top-1/3 right-1/4 w-72 h-72 bg-purple-200 rounded-full mix-blend-multiply filter blur-xl animate-pulse delay-1000"></div>
        <div className="absolute bottom-1/4 left-1/3 w-72 h-72 bg-indigo-200 rounded-full mix-blend-multiply filter blur-xl animate-pulse delay-2000"></div>
      </div>

      {/* Navigation Bar */}
      <nav className="relative z-20 bg-white/80 backdrop-blur-md border-b border-slate-200/50">
        <div className="container mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <div className="w-12 h-12 bg-gradient-to-r  rounded-lg flex items-center justify-center shadow-lg">
                <img src="/src/assets/logo.jpg" />
              </div>
              <span className="text-xl font-bold text-slate-800">HTD PAY</span>
            </div>
            <div className="hidden md:flex items-center space-x-8">
              <a href="#features" className="text-slate-600 hover:text-blue-600 transition-colors font-medium">Tính năng</a>
              <a href="#about" className="text-slate-600 hover:text-blue-600 transition-colors font-medium">Về chúng tôi</a>
              <a href="#testimonials" className="text-slate-600 hover:text-blue-600 transition-colors font-medium">Đánh giá</a>
              <Button 
                onClick={() => setActiveModal('login')}
                variant="ghost" 
                className="text-slate-600 hover:text-blue-600 font-medium"
              >
                Đăng nhập
              </Button>
              <Button 
                onClick={() => setActiveModal('register')}
                className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg shadow-md transition-all duration-200"
              >
                Đăng ký
              </Button>
            </div>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <div className="relative z-10 container mx-auto px-6 py-16">
        {/* Main Hero Content */}
        <div className="text-center mb-20">
          <div className="inline-flex items-center bg-blue-100 text-blue-800 px-4 py-2 rounded-full text-sm font-medium mb-6">
            <Award className="w-4 h-4 mr-2" />
            Ví điện tử được tin dùng nhất Việt Nam 2025
          </div>
          <h1 className="text-5xl md:text-6xl lg:text-7xl font-bold mb-6 text-slate-800 leading-tight">
            Thanh toán thông minh
            <span className="block bg-gradient-to-r from-gray-600 via-indigo-600 to-gray-600 bg-clip-text text-transparent">
              cho thế hệ số
            </span>
          </h1>
          <p className="text-xl md:text-2xl text-slate-600 mb-8 max-w-3xl mx-auto leading-relaxed">
            Trải nghiệm thanh toán không giới hạn với công nghệ bảo mật hàng đầu và giao diện thân thiện
          </p>
          
          {/* CTA Buttons */}
          <div className="flex flex-col sm:flex-row gap-4 justify-center items-center mb-12">
            <Button
              onClick={() => setActiveModal('register')}
              className="bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white font-semibold py-4 px-8 rounded-xl shadow-lg transform hover:scale-105 transition-all duration-200 group"
            >
              <span className="flex items-center">
                Bắt đầu miễn phí
                <ArrowRight className="ml-2 w-5 h-5 group-hover:translate-x-1 transition-transform" />
              </span>
            </Button>
            
            <Button
              onClick={() => setActiveModal('login')}
              variant="outline"
              className="border-2 border-slate-300 text-slate-700 hover:bg-slate-50 font-semibold py-4 px-8 rounded-xl transition-all duration-200"
            >
              Đăng nhập
            </Button>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-8 max-w-4xl mx-auto">
            {stats.map((stat, index) => (
              <div key={index} className="text-center">
                <div className="text-3xl md:text-4xl font-bold text-slate-800 mb-2">{stat.number}</div>
                <div className="text-slate-600 font-medium">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Features Section */}
        <div id="features" className="mb-20">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-4xl font-bold text-slate-800 mb-4">
              Tại sao chọn HTD PAY?
            </h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">
              Những tính năng vượt trội giúp bạn quản lý tài chính hiệu quả và an toàn
            </p>
          </div>
          
          <div className="relative max-w-5xl mx-auto">
            <div className="overflow-hidden rounded-2xl">
              <div 
                className="flex transition-transform duration-500 ease-in-out"
                style={{ transform: `translateX(-${currentFeature * 100}%)` }}
              >
                {features.map((feature, index) => (
                  <div key={index} className="w-full flex-shrink-0 px-4">
                    <div className="bg-white rounded-2xl p-8 shadow-xl border border-slate-200/50 transform hover:scale-105 transition-all duration-300">
                      <div className={`w-16 h-16 bg-gradient-to-r ${feature.color} rounded-2xl flex items-center justify-center mb-6 mx-auto`}>
                        <div className="text-white">
                          {feature.icon}
                        </div>
                      </div>
                      <h3 className="text-2xl font-bold text-center mb-4 text-slate-800">{feature.title}</h3>
                      <p className="text-center text-slate-600 text-lg leading-relaxed">{feature.description}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
            
            {/* Navigation Buttons */}
            <button 
              onClick={prevFeature}
              className="absolute left-2 top-1/2 transform -translate-y-1/2 bg-white hover:bg-slate-50 shadow-lg rounded-full p-3 transition-all duration-200 border border-slate-200"
            >
              <ChevronLeft className="w-6 h-6 text-slate-600" />
            </button>
            <button 
              onClick={nextFeature}
              className="absolute right-2 top-1/2 transform -translate-y-1/2 bg-white hover:bg-slate-50 shadow-lg rounded-full p-3 transition-all duration-200 border border-slate-200"
            >
              <ChevronRight className="w-6 h-6 text-slate-600" />
            </button>

            {/* Dots Indicator */}
            <div className="flex justify-center mt-8 space-x-3">
              {features.map((_, index) => (
                <button
                  key={index}
                  onClick={() => setCurrentFeature(index)}
                  className={`w-3 h-3 rounded-full transition-all duration-300 ${
                    index === currentFeature ? 'bg-blue-600 scale-125' : 'bg-slate-300'
                  }`}
                />
              ))}
            </div>
          </div>
        </div>

        {/* CEO Team Section */}
        <div id="about" className="mb-20">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-4xl font-bold text-slate-800 mb-4">
              Đội ngũ công ty HTD PAY
            </h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">
                Ở vị trí cao nhất, CEO: gương mặt nghiêm nghị, ánh mắt xa xăm như đang tính toán chiến lược triệu đô.
                Phó Giám Đốc: lạnh lùng, ít nói nhưng mỗi lần gật đầu là cả công ty rung chuyển.
                Thư ký riêng: ánh mắt sắc lẹm, chỉ cần một cái liếc cũng đủ ghi chú lại cả cuốn biên bản.
                Trưởng phòng: phong thái cứng rắn, quản lý deadline còn chặt hơn cả quản lý ví tiền.
                Trưởng nhóm: luôn toát ra khí chất “anh cả”, sẵn sàng gánh team qua mọi dự án.
                Và cuối cùng, nhân viên quèn: nụ cười sáng như nắng, công việc tuy nhỏ nhưng chính là người gắn kết cả tập thể.
            </p>
          </div>
          
          <div className="max-w-5xl mx-auto">
            <div className="bg-white rounded-3xl p-8 md:p-12 shadow-xl border border-slate-200/50">
              <div className="flex flex-col lg:flex-row items-center gap-12">
                <div className="flex-shrink-0">
                  <img 
                    src="/src/assets/ceo-team.jpg" 
                    alt="Đội ngũ lãnh đạo HTD PAY" 
                    className="w-80 h-120 object-cover rounded-2xl shadow-xl transform hover:scale-105 transition-all duration-300"
                    onError={(e) => {
                      const target = e.target as HTMLImageElement;
                      target.style.display = 'none';
                      const fallback = target.nextElementSibling as HTMLElement;
                      if (fallback) fallback.style.display = 'flex';
                    }}
                  />
                  <div className="w-80 h-120 bg-gradient-to-br from-slate-200 to-slate-300 rounded-2xl shadow-xl hidden items-center justify-center">
                    <div className="text-center text-slate-500">
                      <Users className="w-16 h-16 mx-auto mb-4" />
                      <p className="text-lg font-medium">Đội ngũ lãnh đạo</p>
                      <p className="text-sm">HTD PAY Team</p>
                    </div>
                  </div>
                </div>
                <div className="flex-1 text-center lg:text-left">
                  <h3 className="text-3xl font-bold mb-6 text-slate-800">
                    Tầm nhìn & Sứ mệnh
                  </h3>
                  <p className="text-slate-600 text-lg mb-8 leading-relaxed">
                    Chúng tôi là đội ngũ chuyên gia công nghệ và tài chính với hơn 15 năm kinh nghiệm. 
                    Sứ mệnh của chúng tôi là democratize financial services, mang đến giải pháp thanh toán 
                    tiên tiến và an toàn cho mọi người dân Việt Nam.
                  </p>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="bg-blue-50 border border-blue-200 px-4 py-3 rounded-xl text-center">
                      <TrendingUp className="w-6 h-6 text-blue-600 mx-auto mb-2" />
                      <div className="text-sm font-semibold text-blue-800">Tăng trưởng bền vững</div>
                    </div>
                    <div className="bg-emerald-50 border border-emerald-200 px-4 py-3 rounded-xl text-center">
                      <Shield className="w-6 h-6 text-emerald-600 mx-auto mb-2" />
                      <div className="text-sm font-semibold text-emerald-800">Bảo mật tuyệt đối</div>
                    </div>
                    <div className="bg-purple-50 border border-purple-200 px-4 py-3 rounded-xl text-center">
                      <Zap className="w-6 h-6 text-purple-600 mx-auto mb-2" />
                      <div className="text-sm font-semibold text-purple-800">Đổi mới liên tục</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Testimonials */}
        <div id="testimonials" className="mb-20">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-4xl font-bold text-slate-800 mb-4">
              Khách hàng nói về chúng tôi
            </h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">
              Hàng triệu người dùng đã tin tưởng và sử dụng HTD PAY
            </p>
          </div>
          
          <div className="relative max-w-4xl mx-auto">
            <div className="overflow-hidden">
              <div 
                className="flex transition-transform duration-500 ease-in-out"
                style={{ transform: `translateX(-${currentTestimonial * 100}%)` }}
              >
                {testimonials.map((testimonial, index) => (
                  <div key={index} className="w-full flex-shrink-0 px-4">
                    <div className="bg-white rounded-2xl p-8 shadow-xl border border-slate-200/50 text-center">
                      <div className="flex justify-center mb-6">
                        {[...Array(testimonial.rating)].map((_, i) => (
                          <Star key={i} className="w-5 h-5 text-amber-400 fill-current" />
                        ))}
                      </div>
                      <p className="text-lg text-slate-600 mb-8 italic leading-relaxed">"{testimonial.content}"</p>
                      <div>
                        <p className="font-semibold text-slate-800 text-lg">{testimonial.name}</p>
                        <p className="text-slate-500">{testimonial.role}</p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Testimonial Navigation */}
            <button 
              onClick={prevTestimonial}
              className="absolute left-2 top-1/2 transform -translate-y-1/2 bg-white hover:bg-slate-50 shadow-lg rounded-full p-3 transition-all duration-200 border border-slate-200"
            >
              <ChevronLeft className="w-6 h-6 text-slate-600" />
            </button>
            <button 
              onClick={nextTestimonial}
              className="absolute right-2 top-1/2 transform -translate-y-1/2 bg-white hover:bg-slate-50 shadow-lg rounded-full p-3 transition-all duration-200 border border-slate-200"
            >
              <ChevronRight className="w-6 h-6 text-slate-600" />
            </button>

            {/* Testimonial Dots */}
            <div className="flex justify-center mt-8 space-x-3">
              {testimonials.map((_, index) => (
                <button
                  key={index}
                  onClick={() => setCurrentTestimonial(index)}
                  className={`w-3 h-3 rounded-full transition-all duration-300 ${
                    index === currentTestimonial ? 'bg-blue-600 scale-125' : 'bg-slate-300'
                  }`}
                />
              ))}
            </div>
          </div>
        </div>

        {/* Final CTA Section */}
        <div className="text-center bg-gradient-to-r from-blue-600 to-indigo-600 rounded-3xl p-12 text-white">
          <h2 className="text-3xl md:text-4xl font-bold mb-4">
            Sẵn sàng bắt đầu?
          </h2>
          <p className="text-xl mb-8 opacity-90">
            Tham gia cùng hàng triệu người dùng đang sử dụng HTD PAY
          </p>
          <Button
            onClick={() => setActiveModal('register')}
            className="bg-white text-blue-600 hover:bg-slate-50 font-semibold py-4 px-8 rounded-xl shadow-lg transform hover:scale-105 transition-all duration-200"
          >
            Tạo tài khoản miễn phí
          </Button>
        </div>

        {/* Footer */}
        <div className="text-center mt-16 text-slate-500">
          <p>© 2024 HTD PAY. Tất cả quyền được bảo lưu. | Được cấp phép bởi Ngân hàng Nhà nước Việt Nam</p>
        </div>
      </div>

      {/* Modals */}
      <LoginModal 
        isOpen={activeModal === 'login'} 
        onClose={() => setActiveModal(null)}
        onSwitchToRegister={() => setActiveModal('register')}
        onForgotPassword={() => setActiveModal('forgot-password')}
      />
      <RegisterModal 
        isOpen={activeModal === 'register'} 
        onClose={() => setActiveModal(null)}
        onSwitchToLogin={() => setActiveModal('login')}
      />
      <ForgotPasswordModal
        isOpen={activeModal === 'forgot-password'}
        onClose={() => setActiveModal('login')}
      />
    </div>
  );
}